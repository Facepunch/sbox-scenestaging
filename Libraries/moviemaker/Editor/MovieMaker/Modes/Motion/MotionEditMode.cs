using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Motion Editor"), Icon( "brush" ), Order( 0 )]
internal sealed partial class MotionEditMode : EditMode
{
	private TimeSelection? _timeSelection;
	private bool _additive;
	private bool _smooth;
	private int _smoothSteps;

	private (FloatSlider Slider, Label Label)? _smoothSlider;

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

	public bool IsAdditive
	{
		get => _additive;

		private set
		{
			_additive = value;
			SelectionChanged();
		}
	}

	public bool SmoothingEnabled
	{
		get => _smooth;

		private set
		{
			_smooth = value;
			SelectionChanged();
		}
	}

	public MovieTime SmoothingSize
	{
		get => _smooth ? Math.Pow( 2d, SmoothingSteps ) / 32d : default;
	}

	public int SmoothingSteps
	{
		get => _smoothSteps;
		private set
		{
			_smoothSteps = Math.Clamp( value, 0, 8 );

			if ( SmoothingEnabled ) SelectionChanged();
		}
	}

	private MovieTime? _selectionStartTime;

	public MotionEditMode()
	{
		History = new EditModeHistory<MotionEditMode>( this );
	}

	protected override void OnEnable()
	{
		Toolbar.AddToggle( "Additive", "layers", () => IsAdditive, state => IsAdditive = state );
		Toolbar.AddToggle( "Smooth", "blur_on", () => SmoothingEnabled, state => SmoothingEnabled = state );

		_smoothSlider = Toolbar.AddSlider( "Smooth Size", () => SmoothingSteps, value => SmoothingSteps = (int)value,
			minimum: 0,
			maximum: 8,
			step: 1,
			getLabel: () => $"{SmoothingSize.TotalSeconds:F2}s" );

		Toolbar.AddSpacingCell();

		Toolbar.AddInterpolationSelector( () => DefaultInterpolation, value =>
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
		if ( !e.LeftMouseButton || !e.HasShift )
		{
			return;
		}

		var time = Session.ScenePositionToTime( DopeSheet.ToScene( e.LocalPosition ) );

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
			if ( time > maxTime ) time = Session.Project!.Duration;

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
