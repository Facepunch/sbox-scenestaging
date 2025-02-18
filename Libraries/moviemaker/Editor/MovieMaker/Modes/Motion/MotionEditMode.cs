namespace Editor.MovieMaker;

#nullable enable

[Title( "Motion Editor"), Icon( "brush" ), Order( 1 )]
internal sealed partial class MotionEditMode : EditMode
{
	private TimeSelectionItem? TimeSelection { get; set; }
	public InterpolationMode DefaultInterpolation { get; private set; } = InterpolationMode.QuadraticInOut;

	public bool IsAdditive { get; private set; }

	private float? _selectionStartTime;

	protected override void OnEnable()
	{
		Toolbar.AddSpacingCell();

		foreach ( var interpolation in Enum.GetValues<InterpolationMode>() )
		{
			Toolbar.AddToggle( interpolation,
				() => (TimeSelection?.Value.Start?.Interpolation ?? DefaultInterpolation) == interpolation,
				_ =>
				{
					DefaultInterpolation = interpolation;

					if ( TimeSelection is { } timeSelection )
					{
						timeSelection.Value = timeSelection.Value.WithInterpolation( interpolation );
						SelectionChanged();
					}
				} );
		}

		Toolbar.AddSpacingCell();
		Toolbar.AddToggle( "Additive", "layers", () => IsAdditive, state => IsAdditive = state );
	}

	private void ClearSelection()
	{
		ClearChanges();

		if ( TimeSelection is not null )
		{
			TimeSelection.Destroy();
			TimeSelection = null;
		}
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

		if ( TimeSelection is null )
		{
			TimeSelection = new TimeSelectionItem( this );
			DopeSheet.Add( TimeSelection );
		}

		TimeSelection.Value = new TimeSelection(
			new FadeTime( time, 0f, DefaultInterpolation ),
			new FadeTime( time, 0f, DefaultInterpolation ) );

		SelectionChanged();

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
				selection.Value = selection.Value.WithTimeRange( null, dragStartTime, DefaultInterpolation );
			}
			else if ( time >= maxTime && time < Session.Clip?.Duration )
			{
				selection.Value = selection.Value.WithTimeRange( dragStartTime, null, DefaultInterpolation );
			}
			else
			{
				selection.Value = selection.Value.WithTimeRange( Math.Min( dragStartTime, time ), Math.Max( dragStartTime, time ), DefaultInterpolation );
			}

			SelectionChanged();
		}
	}

	protected override void OnMouseRelease( MouseEvent e )
	{
		if ( _selectionStartTime is not null && TimeSelection is { } selection )
		{
			_selectionStartTime = null;

			var (minTime, maxTime) = Session.VisibleTimeRange;

			var startTime = Math.Max( minTime, selection.Value.Start?.PeakTime ?? 0f );
			var endTime = Math.Min( maxTime, selection.Value.End?.PeakTime ?? Session.Clip?.Duration ?? 0f );

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

			selection.Value = selection.Value.WithFadeDurationDelta( delta );
			SelectionChanged();

			e.Accept();
		}
	}
}
