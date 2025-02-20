
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

internal readonly record struct TimeSelection( TimeSelection.Fade? FadeIn, TimeSelection.Fade? FadeOut )
{
	internal readonly record struct Fade( MovieTimeRange TimeRange, InterpolationMode Interpolation )
	{
		public MovieTime Start => TimeRange.Start;
		public MovieTime End => TimeRange.End;
		public MovieTime Duration => TimeRange.Duration;
	}

	public bool HasZeroWidthPeak => FadeIn is { } fadeIn && FadeOut is { } fadeOut && fadeIn.End >= fadeOut.Start;

	public MovieTimeRange GetTimeRange( MovieTimeRange limits )
	{
		return (FadeIn?.Start ?? limits.Start, FadeOut?.End ?? limits.End);
	}

	public MovieTimeRange GetTimeRange( MovieClip clip ) => GetTimeRange( (MovieTime.Zero, clip.Duration) );

	public MovieTimeRange GetPeakTimeRange( MovieTimeRange limits )
	{
		return (FadeIn?.End ?? limits.Start, FadeOut?.Start ?? limits.End);
	}

	public MovieTimeRange GetPeakTimeRange( MovieClip clip ) => GetPeakTimeRange( (MovieTime.Zero, clip.Duration) );

	public TimeSelection Clamp( MovieTimeRange timeRange )
	{
		return new TimeSelection(
			FadeIn is { } fadeIn ? fadeIn with { TimeRange = fadeIn.TimeRange.Clamp( timeRange ) } : null,
			FadeOut is { } fadeOut ? fadeOut with { TimeRange = fadeOut.TimeRange.Clamp( timeRange ) } : null );
	}

	public TimeSelection WithInterpolation( InterpolationMode interpolation )
	{
		return new TimeSelection(
			FadeIn is { } fadeIn ? fadeIn with { Interpolation = interpolation } : null,
			FadeOut is { } fadeOut ? fadeOut with { Interpolation = interpolation } : null );
	}

	public TimeSelection WithTimeRange( MovieTime? min, MovieTime? max, InterpolationMode defaultInterpolation )
	{
		return new TimeSelection(
			min is not { } minValue ? null : FadeIn is { } fadeIn ? fadeIn with { TimeRange = minValue } : new Fade( minValue, defaultInterpolation ),
			max is not { } maxValue ? null : FadeOut is { } fadeOut ? fadeOut with { TimeRange = maxValue } : new Fade( maxValue, defaultInterpolation ) );
	}

	public TimeSelection WithFadeDurationDelta( MovieTime delta )
	{
		return new TimeSelection(
			FadeIn is { } fadeIn ? fadeIn with { TimeRange = fadeIn.TimeRange.Grow( delta, MovieTime.Zero ) } : null,
			FadeOut is { } fadeOut ? fadeOut with { TimeRange = fadeOut.TimeRange.Grow( MovieTime.Zero, delta ) } : null );
	}

	public TimeSelection WithPeak( MovieTime time, InterpolationMode defaultInterpolation )
	{
		return new TimeSelection(
			FadeIn is { } fadeIn
				? fadeIn with { TimeRange = (time - fadeIn.Duration, time) }
				: new Fade( time, defaultInterpolation ),
			FadeOut is { } fadeOut
				? fadeOut with { TimeRange = (time, time + fadeOut.Duration) }
				: new Fade( time, defaultInterpolation ) );
	}

	public TimeSelection WithPeakStart( MovieTime time, InterpolationMode defaultInterpolation, bool keepDuration = true )
	{
		if ( FadeIn is not { } fadeIn ) return this with { FadeIn = new Fade( time, defaultInterpolation ) };

		var fadeInStart = keepDuration ? MovieTime.Max( time - fadeIn.Duration, MovieTime.Zero ) : fadeIn.Start;

		time = time.Clamp( (fadeInStart, FadeOut?.TimeRange.Start ?? time) );

		return this with { FadeIn = fadeIn with { TimeRange = (fadeInStart, time) } };
	}

	public TimeSelection WithPeakEnd( MovieTime time, InterpolationMode defaultInterpolation, bool keepDuration = true )
	{
		if ( FadeOut is not { } fadeOut ) return this with { FadeOut = new Fade( time, defaultInterpolation ) };

		var fadeOutEnd = keepDuration ? time + fadeOut.Duration : fadeOut.End;

		time = time.Clamp( (FadeIn?.TimeRange.End ?? time, fadeOutEnd) );

		return this with { FadeOut = fadeOut with { TimeRange = (time, fadeOutEnd) } };
	}

	public TimeSelection WithFadeStart( MovieTime time )
	{
		if ( FadeIn is not { } fadeIn ) return this;

		return this with
		{
			FadeIn = fadeIn with { TimeRange = (time.Clamp( (MovieTime.Zero, fadeIn.End) ), fadeIn.End) }
		};
	}

	public TimeSelection WithFadeEnd( MovieTime time )
	{
		if ( FadeOut is not { } fadeOut ) return this;

		return this with
		{
			FadeOut = fadeOut with { TimeRange = (fadeOut.Start, MovieTime.Max( fadeOut.Start, time )) }
		};
	}

	public float GetFadeValue( MovieTime time )
	{
		if ( FadeIn is { } fadeIn && time < fadeIn.End )
		{
			return fadeIn.Interpolation.Apply( (float)fadeIn.TimeRange.GetFraction( time ) );
		}

		if ( FadeOut is { } fadeOut && time > fadeOut.Start )
		{
			return fadeOut.Interpolation.Apply( 1f - (float)fadeOut.TimeRange.GetFraction( time ) );
		}

		return 1f;
	}
}

partial class MotionEditMode
{
	private interface ITimeSelectionItem
	{
		void UpdatePosition( TimeSelection value, Rect viewRect );
		void Destroy();
	}

	private sealed class TimeSelectionPeakItem : GraphicsItem, ITimeSelectionItem
	{
		public MotionEditMode EditMode { get; }

		public TimeSelectionPeakItem( MotionEditMode editMode )
		{
			EditMode = editMode;

			ZIndex = 10000;

			Movable = true;
			Cursor = CursorShape.Finger;
		}

		protected override void OnMoved()
		{
			if ( EditMode.TimeSelection is not { } value ) return;

			if ( value.FadeIn is { } fadeIn )
			{
				var time = EditMode.Session.PixelsToTime( Position.x, true );

				value = value with { FadeIn = fadeIn with { TimeRange = (time - fadeIn.Duration, time) } };
			}

			if ( value.FadeOut is { } fadeOut )
			{
				var time = EditMode.Session.PixelsToTime( Position.x + Size.x, true );

				value = value with { FadeOut = fadeOut with { TimeRange = (time, time + fadeOut.Duration) } };
			}

			EditMode.TimeSelection = value;
		}

		public void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			var timeRange = value.GetPeakTimeRange( EditMode.Session.Clip! );

			Position = new Vector2( EditMode.Session.TimeToPixels( timeRange.Start ), viewRect.Top );
			Size = new Vector2( EditMode.Session.TimeToPixels( timeRange.Duration ), viewRect.Height );

			Update();
		}

		protected override void OnPaint()
		{
			var color = EditMode.SelectionColor;

			Paint.Antialiasing = true;

			Paint.SetBrush( color );
			Paint.SetPen( Color.White.WithAlpha( 0.5f ), 0.5f );
			Paint.DrawRect( LocalRect.Grow( 0f, 16f ) );
		}
	}

	private enum FadeKind
	{
		FadeIn,
		FadeOut
	}

	private sealed class TimeSelectionFadeItem : GraphicsItem, ITimeSelectionItem
	{
		public MotionEditMode EditMode { get; }
		public FadeKind Kind { get; }

		public TimeSelection.Fade? Value
		{
			get => Kind == FadeKind.FadeIn ? EditMode.TimeSelection?.FadeIn : EditMode.TimeSelection?.FadeOut;
			set
			{
				if ( EditMode.TimeSelection is not { } selection ) return;

				if ( Kind == FadeKind.FadeIn )
				{
					EditMode.TimeSelection = selection with { FadeIn = value };
				}
				else
				{
					EditMode.TimeSelection = selection with { FadeOut = value };
				}
			}
		}

		public TimeSelectionFadeItem( MotionEditMode editMode, FadeKind kind )
		{
			EditMode = editMode;
			Kind = kind;

			ZIndex = 10001;

			HandlePosition = kind == FadeKind.FadeIn ? new Vector2( 0f, 0f ) : new Vector2( 1f, 0f );

			Movable = true;
			HoverEvents = true;
			Focusable = true;

			Cursor = CursorShape.Finger;
		}

		public void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			if ( Value is not { } fade )
			{
				Position = new Vector2( -50000f, 0f );
			}
			else
			{
				Position = new Vector2( EditMode.Session.TimeToPixels( Kind == FadeKind.FadeIn ? fade.Start : fade.End ), viewRect.Top );
				Size = new Vector2( EditMode.Session.TimeToPixels( fade.Duration ), viewRect.Height );
			}

			Update();
		}

		protected override void OnMoved()
		{
			if ( EditMode.TimeSelection is not { } selection || Value is not { } value ) return;

			if ( selection.HasZeroWidthPeak )
			{
				var offset = HandlePosition.x * 2f - 1f;
				var time = EditMode.Session.PixelsToTime( Position.x - offset * Width, true );
				EditMode.TimeSelection = selection.WithPeak( time, EditMode.DefaultInterpolation );
			}
			else
			{
				var time = EditMode.Session.PixelsToTime( Position.x, true );

				EditMode.TimeSelection = Kind switch
				{
					FadeKind.FadeIn => selection.WithPeakStart( time + value.TimeRange.Duration, value.Interpolation ),
					FadeKind.FadeOut => selection.WithPeakEnd( time - value.TimeRange.Duration, value.Interpolation ),
					_ => selection
				};
			}
		}

		protected override void OnKeyPress( KeyEvent e )
		{
			if ( Value is not { } value ) return;

			if ( e.Key is >= KeyCode.Num0 and <= KeyCode.Num9 )
			{
				var modes = Enum.GetValues<InterpolationMode>();
				var index = e.Key - KeyCode.Num0;

				if ( index >= 0 && index < modes.Length )
				{
					Value = value with { Interpolation = modes[index] };

					e.Accepted = true;
					return;
				}
			}
		}

		private readonly List<Vector2> _points = new();

		protected override void OnPaint()
		{
			if ( Width < 1f || Value is not { } value ) return;

			var color = EditMode.SelectionColor;

			if ( Hovered ) color = color.Lighten( 0.1f );

			var fadeColor = color.WithAlpha( 0.02f );
			var scrubBarHeight = EditMode.DopeSheet.ScrubBarTop.Height;

			var (x0, x1) = Kind == FadeKind.FadeIn
				? (0f, Width)
				: (Width, 0f);

			_points.Clear();

			AddPoints( _points, value.Interpolation, new Vector2( x0, LocalRect.Top + scrubBarHeight ), new Vector2( x1, LocalRect.Top ), false );
			AddPoints( _points, value.Interpolation, new Vector2( x1, LocalRect.Bottom ), new Vector2( x0, LocalRect.Bottom - scrubBarHeight ), true );

			Paint.Antialiasing = true;

			Paint.SetBrushLinear( new Vector2( x0, 0f ), new Vector2( x1, 0f ), fadeColor, color );
			Paint.SetPen( color.WithAlpha( 0.5f ), 0.5f );
			Paint.DrawPolygon( _points );
		}

		private void AddPoints( List<Vector2> points, InterpolationMode interpolation, Vector2 start, Vector2 end, bool flip )
		{
			var pointCount = (int)Math.Clamp( Width / 4f, 2f, 32f );

			for ( var i = 0; i <= pointCount; ++i )
			{
				var t = (float)i / pointCount;
				var v = flip ? 1f - interpolation.Apply( 1f - t ) : interpolation.Apply( t );

				points.Add( start + new Vector2( t * (end.x - start.x), v * (end.y - start.y) ) );
			}
		}
	}

	private sealed class TimeSelectionHandleItem : GraphicsItem, ITimeSelectionItem
	{
		public MotionEditMode EditMode { get; }

		private readonly Func<TimeSelection, MovieTime?> _getTime;
		private readonly Func<TimeSelection, MovieTime, TimeSelection> _setTime;

		public TimeSelectionHandleItem( MotionEditMode editMode, Func<TimeSelection, MovieTime?> getTime, Func<TimeSelection, MovieTime, TimeSelection> setTime )
		{
			EditMode = editMode;

			Movable = true;
			HoverEvents = true;

			Cursor = CursorShape.SizeH;
			HandlePosition = new Vector2( 0.5f, 0f );

			ZIndex = 10002;

			_getTime = getTime;
			_setTime = setTime;
		}

		protected override void OnMoved()
		{
			var time = EditMode.Session.PixelsToTime( Position.x, true );

			if ( EditMode.TimeSelection is { } selection )
			{
				EditMode.TimeSelection = _setTime( selection, time );
			}
		}

		public void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			if ( _getTime( value ) is { } time )
			{
				Position = new Vector2( EditMode.Session.TimeToPixels( time ), viewRect.Top );
			}
			else
			{
				Position = new Vector2( -50000f, 0f );
			}

			Size = new Vector2( 4f, viewRect.Height );
		}
	}
}
