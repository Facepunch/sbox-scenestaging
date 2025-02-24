using Editor.MapEditor;
using Sandbox;

namespace Editor.MovieMaker;

internal record struct FadeTime( float PeakTime, float Duration, InterpolationMode Interpolation );

internal record struct TimeSelection( FadeTime? Start, FadeTime? End )
{
	public TimeSelection Clamp( float minTime, float maxTime )
	{
		return new TimeSelection(
			Start is { } start ? start with { PeakTime = Math.Max( minTime, start.PeakTime ) } : null,
			End is { } end ? end with { PeakTime = Math.Min( maxTime, end.PeakTime ) } : null );
	}

	public TimeSelection WithInterpolation( InterpolationMode interpolation )
	{
		return new TimeSelection(
			Start is { } start ? start with { Interpolation = interpolation } : null,
			End is { } end ? end with { Interpolation = interpolation } : null );
	}

	public TimeSelection WithTimeRange( float min, float max, InterpolationMode defaultInterpolation )
	{
		if ( min > max )
		{
			(min, max) = (max, min);
		}

		return new TimeSelection(
			Start is { } start ? start with { PeakTime = min } : new FadeTime( min, 0f, defaultInterpolation ),
			End is { } end ? end with { PeakTime = max } : new FadeTime( max, 0f, defaultInterpolation ) );
	}

	public TimeSelection WithFadeDurationDelta( float delta )
	{
		return new TimeSelection(
			Start is { } start ? start with { Duration = Math.Max( 0f, start.Duration + delta ) } : null,
			End is { } end ? end with { Duration = Math.Max( 0f, end.Duration + delta ) } : null );
	}

	public bool Overlaps( float minTime, float maxTime )
	{
		if ( Start is { } start && start.PeakTime - start.Duration > maxTime ) return false;
		if ( End is { } end && end.PeakTime + end.Duration < minTime ) return false;

		return true;
	}

	public float GetFadeValue( float time )
	{
		if ( Start is { } start && time < start.PeakTime )
		{
			return time > start.PeakTime - start.Duration
				? start.Interpolation.Apply( (time - start.PeakTime + start.Duration) / start.Duration )
				: 0f;
		}

		if ( End is { } end && time > end.PeakTime )
		{
			return time < end.PeakTime + end.Duration
				? end.Interpolation.Apply( (end.PeakTime + end.Duration - time) / end.Duration )
				: 0f;
		}

		return 1f;
	}
}

partial class MotionEditMode
{
	private sealed class TimeSelectionItem : GraphicsItem
	{
		public MotionEditMode EditMode { get; }

		private TimeSelection _value;
		private bool _hasChanges;

		public TimeSelection Value
		{
			get => _value;
			set
			{
				_value = value.Clamp( 0f, EditMode.Session.Clip?.Duration ?? float.PositiveInfinity );
				UpdatePosition();
			}
		}

		public bool HasChanges
		{
			get => _hasChanges;
			set
			{
				_hasChanges = value;
				UpdatePosition();
			}
		}

		public Color Color => (HasChanges ? Theme.Yellow : Theme.Blue).WithAlpha( 0.25f );

		public TimeSelectionItem( MotionEditMode editMode )
		{
			EditMode = editMode;

			ZIndex = 10000;

			Movable = true;
			Cursor = CursorShape.Finger;

			EditMode.Session.ViewChanged += Session_ViewChanged;
		}

		protected override void OnMoved()
		{
			var startTime = EditMode.Session.PixelsToTime( Position.x );
			var endTime = EditMode.Session.PixelsToTime( Position.x + Size.x );

			var value = Value;

			if ( value.Start is { } start )
			{
				value = value with { Start = start with { PeakTime = startTime + start.Duration } };
			}

			if ( value.End is { } end )
			{
				value = value with { End = end with { PeakTime = endTime - end.Duration } };
			}

			Value = value;
			EditMode.SelectionChanged();
		}

		protected override void OnDestroy()
		{
			EditMode.Session.ViewChanged -= Session_ViewChanged;
		}

		private void Session_ViewChanged()
		{
			UpdatePosition();
		}

		private void UpdatePosition()
		{
			PrepareGeometryChange();

			var startTime = Value.Start?.PeakTime ?? 0f;
			var endTime = Value.End?.PeakTime ?? EditMode.Session.Clip?.Duration ?? 0f;

			startTime -= Value.Start?.Duration ?? 0f;
			endTime += Value.End?.Duration ?? 0f;

			Position = new Vector2( EditMode.Session.TimeToPixels( startTime ), 0f );
			Size = new Vector2( EditMode.Session.TimeToPixels( endTime - startTime ), EditMode.DopeSheet.Height );

			Update();

			EditMode.Session.Editor.ScrubBarTop.Update();
			EditMode.Session.Editor.ScrubBarBottom.Update();
		}

		protected override void OnPaint()
		{
			var color = Color;
			var fadeInWidth = EditMode.Session.TimeToPixels( Value.Start?.Duration ?? 0f );
			var fadeOutWidth = EditMode.Session.TimeToPixels( Value.End?.Duration ?? 0f );

			Paint.Antialiasing = true;

			Paint.ClearPen();

			if ( fadeInWidth > 0f )
			{
				Paint.SetBrushLinear( new Vector2( 0f, 0f ), new Vector2( fadeInWidth, 0f ), color.WithAlpha( 0.02f ), color );
				Paint.DrawRect( new Rect( 0f, 0f, fadeInWidth, LocalRect.Height ) );
			}

			Paint.SetBrush( color );
			Paint.DrawRect( new Rect( fadeInWidth, 0f, LocalRect.Width - fadeInWidth - fadeOutWidth, LocalRect.Height ) );

			if ( fadeOutWidth > 0f )
			{
				Paint.SetBrushLinear( new Vector2( LocalRect.Width - fadeOutWidth, 0f ), new Vector2( LocalRect.Width, 0f ), color, color.WithAlpha( 0.02f ) );
				Paint.DrawRect( new Rect( LocalRect.Width - fadeOutWidth, 0f, fadeOutWidth, LocalRect.Height ) );
			}

			Paint.ClearBrush();
			Paint.SetPen( color.WithAlpha( 0.5f ), 0.5f );
			Paint.DrawLine( new Vector2( 0f, 0f ), new Vector2( 0f, LocalRect.Height ) );
			Paint.DrawLine( new Vector2( LocalRect.Width, 0f ), new Vector2( LocalRect.Width, LocalRect.Height ) );

			Paint.SetPen( Color.White.WithAlpha( 0.5f ), 0.5f );
			Paint.DrawLine( new Vector2( fadeInWidth, 0f ), new Vector2( fadeInWidth, LocalRect.Height ) );
			Paint.DrawLine( new Vector2( LocalRect.Width - fadeOutWidth, 0f ), new Vector2( LocalRect.Width - fadeOutWidth, LocalRect.Height ) );
		}

		private List<Vector2> TempPoints { get; } = new();

		public void ScrubberPaint( ScrubberWidget scrubber )
		{
			var x1 = scrubber.ToPixels( Value.Start?.PeakTime ?? 0f );
			var x2 = scrubber.ToPixels( Value.End?.PeakTime ?? EditMode.Session.Clip?.Duration ?? 0f );

			var x0 = x1 - EditMode.Session.TimeToPixels( Value.Start?.Duration ?? 0f );
			var x3 = x2 + EditMode.Session.TimeToPixels( Value.End?.Duration ?? 0f );

			var y0 = scrubber.IsTop ? scrubber.LocalRect.Bottom : scrubber.LocalRect.Top;
			var y1 = scrubber.IsTop ? scrubber.LocalRect.Top : scrubber.LocalRect.Bottom;

			var points = TempPoints;

			points.Clear();

			if ( Value.Start is { } start )
			{
				AddCurve( points,
					new Vector2( x0, y0 ),
					new Vector2( x1 - x0, y1 - y0 ),
					start.Interpolation,
					false );
			}

			if ( Value.End is { } end )
			{
				AddCurve( points,
					new Vector2( x2, y1 ),
					new Vector2( x3 - x2, y0 - y1 ),
					end.Interpolation,
					true );
			}

			Paint.SetBrushAndPen( Color );
			Paint.DrawPolygon( points );

			Paint.SetPen( Color.WithAlpha( 0.5f ), 1f );
			Paint.DrawLine( points );

			Paint.SetPen( Color.White.WithAlpha( 0.5f ), 0.5f );
			Paint.DrawLine( new Vector2( x1, 0f ), new Vector2( x1, LocalRect.Height ) );
			Paint.DrawLine( new Vector2( x2, 0f ), new Vector2( x2, LocalRect.Height ) );
		}

		private void AddCurve( List<Vector2> points, Vector2 origin, Vector2 delta, InterpolationMode interpolation, bool flip )
		{
			const int steps = 16;

			for ( var i = 0; i <= steps; ++i )
			{
				var t = (float)i / steps;
				var x = origin.x + t * delta.x;
				var y = origin.y + (flip ? 1f - interpolation.Apply( 1f - t ) : interpolation.Apply( t )) * delta.y;

				points.Add( new Vector2( x, y ) );
			}
		}
	}
}
