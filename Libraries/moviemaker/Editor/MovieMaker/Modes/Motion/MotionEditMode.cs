using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Motion Editor" ), Icon( "brush" ), Order( 1 )]
[Description( "Sculpt changes on selected time ranges. Ideal for tweaking recordings." )]
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

	protected override void OnEnable()
	{
		var saveSequenceDisplay = new ToolBarItemDisplay( "Save As Sequence..", "theaters",
			"Save the time selection as a new movie project, and reference it in this timeline as a sequence block." );

		var editGroup = ToolBar.AddGroup();

		var insertDisplay = new ToolBarItemDisplay( "Insert", "keyboard_tab",
			"Insert time inside the selected range, right-shifting track data after the insertion point." );

		var removeDisplay = new ToolBarItemDisplay( "Remove", "backspace",
			"Delete track data inside the selected range, left-shifting everything after the deletion." );

		var clearDisplay = new ToolBarItemDisplay( "Clear", "delete",
			"Delete track data inside the selected range, without shifting anything after the deletion. This will leave an empty range without track data." );

		editGroup.AddAction( insertDisplay, Insert, () => TimeSelection is not null );
		editGroup.AddAction( removeDisplay, () => Delete( true ), () => TimeSelection is not null );
		editGroup.AddAction( clearDisplay, () => Delete( false ), () => TimeSelection is not null );

		editGroup.AddAction( saveSequenceDisplay,
			() => Session.Editor.SaveAsDialog( "Save As Sequence..", () => CreateSequence( TimeSelection!.Value.TotalTimeRange ) ),
			() => TimeSelection is not null );

		ToolBarGroup? customGroup = null;

		var modificationTypes = EditorTypeLibrary
			.GetTypesWithAttribute<MovieModificationAttribute>()
			.OrderBy( x => x.Attribute.Order );

		foreach ( var (type, attribute) in modificationTypes )
		{
			if ( type.IsAbstract || type.IsGenericType ) continue;

			customGroup ??= ToolBar.AddGroup();

			var display = new ToolBarItemDisplay( attribute.Title, attribute.Icon, attribute.Description );

			var toggle = customGroup.AddToggle( display,
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

		var selectionGroup = ToolBar.AddGroup();

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

		var scenePos = Timeline.ToScene( e.LocalPosition );

		if ( Timeline.GetItemAt( scenePos ) is TimeSelectionItem && !e.HasShift ) return;

		var time = Session.ScenePositionToTime( scenePos );

		_selectionStartTime = Session.ScenePositionToTime( scenePos );
		_newTimeSelection = false;

		Session.PlayheadTime = time;

		e.Accepted = true;
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( (e.ButtonState & MouseButtons.Left) == 0 ) return;
		if ( _selectionStartTime is not { } dragStartTime ) return;

		e.Accepted = true;

		var time = Session.ScenePositionToTime( Timeline.ToScene( e.LocalPosition ), SnapFlag.Selection );

		// Only create a time selection when mouse has moved enough

		if ( time == dragStartTime && TimeSelection is null ) return;

		var (minTime, maxTime) = Session.VisibleTimeRange;

		if ( time < minTime ) time = MovieTime.Zero;
		if ( time > maxTime ) time = Session.Project!.Duration;

		TimeSelection = new TimeSelection( (MovieTime.Min( time, dragStartTime ), MovieTime.Max( time, dragStartTime )), DefaultInterpolation );
		_newTimeSelection = true;

		Session.PreviewTime = time;
	}

	protected override void OnMouseRelease( MouseEvent e )
	{
		if ( _selectionStartTime is null ) return;
		_selectionStartTime = null;

		if ( !_newTimeSelection ) return;
		_newTimeSelection = false;

		if ( TimeSelection is not { } selection ) return;

		var timeRange = selection.PeakTimeRange.Clamp( Session.VisibleTimeRange );

		Session.PlayheadTime = MovieTime.FromTicks( (timeRange.Start.Ticks + timeRange.End.Ticks) / 2 );
		Session.PreviewTime = null;
	}

	protected override void OnMouseWheel( WheelEvent e )
	{
		if ( TimeSelection is not { } oldSelection || !oldSelection.PeakTimeRange.Contains( Session.PlayheadTime ) )
		{
			TimeSelection = new TimeSelection( Session.PlayheadTime, DefaultInterpolation );
		}

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

		if ( Timeline.GetItemAt( Timeline.ToScene( Timeline.FromScreen( Application.CursorPosition ) ) ) is TimeSelectionFadeItem fade )
		{
			fade.Interpolation = mode;
		}
	}

	protected override void OnGetSnapTimes( ref TimeSnapHelper snapHelper )
	{
		if ( TimeSelection is not { } selection ) return;

		snapHelper.Add( SnapFlag.SelectionTotalStart, selection.TotalStart );
		snapHelper.Add( SnapFlag.SelectionPeakStart, selection.PeakStart );
		snapHelper.Add( SnapFlag.SelectionPeakEnd, selection.PeakEnd );
		snapHelper.Add( SnapFlag.SelectionTotalEnd, selection.TotalEnd );
	}

	protected override Color GetTrailColor( MovieTime time )
	{
		if ( TimeSelection is not { } selection ) return base.GetTrailColor( time );

		return Color.Gray.LerpTo( SelectionColor, selection.GetFadeValue( time ) );
	}
}
