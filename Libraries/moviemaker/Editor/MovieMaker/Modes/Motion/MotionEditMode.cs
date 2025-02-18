using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

[Title( "Motion Editor"), Icon( "brush" ), Order( 1 )]
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
	public InterpolationMode DefaultInterpolation { get; private set; } = InterpolationMode.QuadraticInOut;

	public bool IsAdditive { get; private set; }

	private float? _selectionStartTime;

	protected override void OnEnable()
	{
		Toolbar.AddSpacingCell();

		foreach ( var interpolation in Enum.GetValues<InterpolationMode>() )
		{
			Toolbar.AddToggle( interpolation,
				() => (TimeSelection?.Start?.Interpolation ?? DefaultInterpolation) == interpolation,
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
		if ( !e.LeftMouseButton || !e.HasShift || Session.PreviewPointer is not { } time )
		{
			return;
		}

		TimeSelection = new TimeSelection(
			new FadeSelection( time, time, DefaultInterpolation ),
			new FadeSelection( time, time, DefaultInterpolation ) );

		_selectionStartTime = time;

		e.Accepted = true;
		return;
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( (e.ButtonState & MouseButtons.Left) != 0 && e.HasShift
			&& TimeSelection is { } selection
			&& Session.PreviewPointer is { } time
			&& _selectionStartTime is { } dragStartTime )
		{
			var (minTime, maxTime) = Session.VisibleTimeRange;

			if ( time <= minTime )
			{
				TimeSelection = selection.WithTimeRange( null, dragStartTime, DefaultInterpolation );
			}
			else if ( time >= maxTime && time < Session.Clip?.Duration )
			{
				TimeSelection = selection.WithTimeRange( dragStartTime, null, DefaultInterpolation );
			}
			else
			{
				TimeSelection = selection.WithTimeRange( Math.Min( dragStartTime, time ), Math.Max( dragStartTime, time ), DefaultInterpolation );
			}
		}
	}

	protected override void OnMouseRelease( MouseEvent e )
	{
		if ( _selectionStartTime is not null && TimeSelection is { } selection )
		{
			_selectionStartTime = null;

			var (minTime, maxTime) = Session.VisibleTimeRange;

			var startTime = Math.Max( minTime, selection.Start?.PeakTime ?? 0f );
			var endTime = Math.Min( maxTime, selection.End?.PeakTime ?? Session.Clip?.Duration ?? 0f );

			var midTime = (startTime + endTime) * 0.5f;

			Session.SetCurrentPointer( midTime );
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

		if ( DopeSheet.GetItemAt( DopeSheet.ToScene( DopeSheet.FromScreen( Application.CursorPosition ) ) ) is TimeSelectionFadeItem { Value: { } value } fade )
		{
			fade.Value = value with { Interpolation = mode };
		}
	}
}
