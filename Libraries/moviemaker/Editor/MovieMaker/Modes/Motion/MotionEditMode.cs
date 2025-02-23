using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Motion Editor"), Icon( "brush" ), Order( 0 )]
internal sealed partial class MotionEditMode : EditMode
{
	private TimeSelection? _timeSelection;

	public TimeSelection? TimeSelection
	{
		get => _timeSelection;
		set
		{
			_timeSelection = value;
			SelectionChanged();
		}
	}

	public MovieTime ChangeOffset { get; set; }

	public InterpolationMode DefaultInterpolation { get; private set; } = InterpolationMode.QuadraticInOut;

	public bool IsAdditive { get; private set; }

	private MovieTime? _selectionStartTime;

	protected override void OnEnable()
	{
		Toolbar.AddSpacingCell();

		foreach ( var interpolation in Enum.GetValues<InterpolationMode>() )
		{
			Toolbar.AddToggle( interpolation,
				() => (TimeSelection?.FadeIn.Interpolation ?? DefaultInterpolation) == interpolation,
				_ =>
				{
					DefaultInterpolation = interpolation;

					if ( TimeSelection is { } timeSelection )
					{
						TimeSelection = timeSelection.WithInterpolation( interpolation );
					}
				} );
		}

		Toolbar.AddSpacingCell();
		Toolbar.AddToggle( "Additive", "layers", () => IsAdditive, state => IsAdditive = state );
	}

	private void ClearSelection()
	{
		ClearChanges();

		TimeSelection = null;
	}

	protected override void OnDisable()
	{
		ClearSelection();
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( !e.LeftMouseButton || !e.HasShift )
		{
			return;
		}

		var time = Session.ScenePositionToTime( DopeSheet.ToScene( e.LocalPosition ) );

		ClearSelection();

		Session.SetPreviewPointer( time );

		TimeSelection = new TimeSelection( time, DefaultInterpolation );

		_selectionStartTime = time;

		e.Accepted = true;
		return;
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( (e.ButtonState & MouseButtons.Left) != 0 && e.HasShift
			&& _selectionStartTime is { } dragStartTime )
		{
			var time = Session.ScenePositionToTime( DopeSheet.ToScene( e.LocalPosition ), ignore: SnapFlag.Selection );
			var (minTime, maxTime) = Session.VisibleTimeRange;

			if ( time < minTime ) time = MovieTime.Zero;
			if ( time > maxTime ) time = Session.Clip!.Duration;

			TimeSelection = new TimeSelection( (MovieTime.Min( time, dragStartTime ), MovieTime.Max( time, dragStartTime )), DefaultInterpolation );
		}
	}

	protected override void OnMouseRelease( MouseEvent e )
	{
		if ( _selectionStartTime is not null && TimeSelection is { } selection )
		{
			_selectionStartTime = null;

			var timeRange = selection.PeakTimeRange.Clamp( Session.VisibleTimeRange );

			Session.SetCurrentPointer( MovieTime.FromTicks( (timeRange.Start.Ticks + timeRange.End.Ticks) / 2 ) );
			Session.ClearPreviewPointer();
		}
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
