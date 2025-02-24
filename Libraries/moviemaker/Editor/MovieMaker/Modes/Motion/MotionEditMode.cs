using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Motion Editor"), Icon( "brush" ), Order( 0 )]
public sealed partial class MotionEditMode : EditMode
{
	private TimeSelection? _timeSelection;
	private bool _newTimeSelection;

	public TimeSelection? TimeSelection
	{
		get => _timeSelection;
		set
		{
			_timeSelection = value;
			SelectionChanged();
		}
	}

	public InterpolationMode DefaultInterpolation { get; private set; } = InterpolationMode.QuadraticInOut;

	private MovieTime? _selectionStartTime;

	public MotionEditMode()
	{
		History = new EditModeHistory<MotionEditMode>( this );
	}

	protected override void OnEnable()
	{
		var undoGroup = Toolbar.AddGroup();

		var session = Session;

		undoGroup.AddAction( "Undo", "undo", session.Undo, () => session.History.CanUndo );
		undoGroup.AddAction( "Redo", "redo", session.Redo, () => session.History.CanRedo );

		var clipboardGroup = Toolbar.AddGroup();

		clipboardGroup.AddAction( "Cut", "content_cut", Cut, () => TimeSelection is not null );
		clipboardGroup.AddAction( "Copy", "content_copy", Copy, () => TimeSelection is not null );
		clipboardGroup.AddAction( "Paste", "content_paste", Paste, () => Clipboard is not null );
		clipboardGroup.AddAction( "Save As Sequence..", "theaters",
			() => Session.Editor.SaveAsDialog( "Save As Sequence..",
				() => CreateSequence( TimeSelection!.Value.TotalTimeRange ) ),
				() => TimeSelection is not null );

		var editGroup = Toolbar.AddGroup();

		editGroup.AddAction( "Insert", "keyboard_tab", Insert, () => TimeSelection is not null );
		editGroup.AddAction( "Remove", "backspace", () => Delete( true ), () => TimeSelection is not null );
		editGroup.AddAction( "Clear", "delete", () => Delete( false ), () => TimeSelection is not null );

		ToolbarGroup? customGroup = null;

		var modificationTypes = EditorTypeLibrary
			.GetTypesWithAttribute<MovieModificationAttribute>()
			.OrderBy( x => x.Attribute.Order );

		foreach ( var (type, attribute) in modificationTypes )
		{
			if ( type.IsAbstract || type.IsGenericType ) continue;

			customGroup ??= Toolbar.AddGroup();

			var toggle = customGroup.AddToggle( attribute.Title, attribute.Icon,
				() => Modification?.GetType() == type.TargetType,
				value =>
				{
					if ( value && TimeSelection is { } selection )
					{
						var modification = SetModification( type.TargetType, selection );

						modification.Start( selection );
					}
					else if ( !value )
					{
						ClearChanges();
					}
				} );

			toggle.Bind( nameof(IconButton.Enabled) )
				.ReadOnly()
				.From( () => TimeSelection is not null, (Action<bool>?)null );
		}

		var selectionGroup = Toolbar.AddGroup();

		selectionGroup.AddInterpolationSelector( () => DefaultInterpolation, value =>
		{
			DefaultInterpolation = value;

			if ( TimeSelection is { } timeSelection )
			{
				TimeSelection = timeSelection.WithInterpolation( value );
			}
		} );

		SelectionChanged();
	}

	protected override void OnDisable()
	{
		ClearChanges();

		TimeSelection = null;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( !e.LeftMouseButton ) return;

		var scenePos = DopeSheet.ToScene( e.LocalPosition );

		if ( DopeSheet.GetItemAt( scenePos ) is TimeSelectionItem && !e.HasShift ) return;

		_selectionStartTime = Session.ScenePositionToTime( scenePos );
		_newTimeSelection = false;

		e.Accepted = true;
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( (e.ButtonState & MouseButtons.Left) == 0 ) return;
		if ( _selectionStartTime is not { } dragStartTime ) return;

		e.Accepted = true;

		var time = Session.ScenePositionToTime( DopeSheet.ToScene( e.LocalPosition ), SnapFlag.Selection );

		// Only create a time selection when mouse has moved enough

		if ( time == dragStartTime && TimeSelection is null ) return;

		var (minTime, maxTime) = Session.VisibleTimeRange;

		if ( time < minTime ) time = MovieTime.Zero;
		if ( time > maxTime ) time = Session.Project!.Duration;

		TimeSelection = new TimeSelection( (MovieTime.Min( time, dragStartTime ), MovieTime.Max( time, dragStartTime )), DefaultInterpolation );
		_newTimeSelection = true;

		Session.SetPreviewPointer( time );
	}

	protected override void OnMouseRelease( MouseEvent e )
	{
		if ( _selectionStartTime is null ) return;
		_selectionStartTime = null;

		if ( !_newTimeSelection ) return;
		_newTimeSelection = false;

		if ( TimeSelection is not { } selection ) return;

		var timeRange = selection.PeakTimeRange.Clamp( Session.VisibleTimeRange );

		Session.SetCurrentPointer( MovieTime.FromTicks( (timeRange.Start.Ticks + timeRange.End.Ticks) / 2 ) );
		Session.ClearPreviewPointer();
	}

	protected override void OnMouseWheel( WheelEvent e )
	{
		if ( TimeSelection is { } selection && e.HasShift )
		{
			var delta = Math.Sign( e.Delta ) * Session.MinorTick.Interval;

			TimeSelection = selection.WithFadeDurationDelta( delta );

			e.Accept();
		}
	}

	private void SetInterpolation( InterpolationMode mode )
	{
		DefaultInterpolation = mode;

		if ( DopeSheet.GetItemAt( DopeSheet.ToScene( DopeSheet.FromScreen( Application.CursorPosition ) ) ) is TimeSelectionFadeItem fade )
		{
			fade.Interpolation = mode;
		}
	}

	protected override void OnGetSnapTimes( ref TimeSnapHelper snapHelper )
	{
		if ( TimeSelection is { } selection )
		{
			snapHelper.Add( SnapFlag.SelectionTotalStart, selection.TotalStart );
			snapHelper.Add( SnapFlag.SelectionPeakStart, selection.PeakStart );
			snapHelper.Add( SnapFlag.SelectionPeakEnd, selection.PeakEnd );
			snapHelper.Add( SnapFlag.SelectionTotalEnd, selection.TotalEnd );
		}
	}
}
