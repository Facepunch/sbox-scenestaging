
namespace Editor.MovieMaker;

internal readonly record struct FadeSelection( float PeakTime, float FadeTime, InterpolationMode Interpolation )
{
	public float Duration => Math.Abs( PeakTime - FadeTime );
}

internal readonly record struct TimeSelection( FadeSelection? Start, FadeSelection? End )
{
	public bool HasZeroWidthPeak => Start is { } start && End is { } end && start.PeakTime >= end.PeakTime - 0.001f;

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

	public TimeSelection WithTimeRange( float? min, float? max, InterpolationMode defaultInterpolation )
	{
		return new TimeSelection(
			min is not { } minValue ? null : Start is { } start ? start with { PeakTime = minValue, FadeTime = minValue } : new FadeSelection( minValue, minValue, defaultInterpolation ),
			max is not { } maxValue ? null : End is { } end ? end with { PeakTime = maxValue, FadeTime = maxValue } : new FadeSelection( maxValue, maxValue, defaultInterpolation ) );
	}

	public TimeSelection WithFadeDurationDelta( float delta )
	{
		return new TimeSelection(
			Start is { } start ? start with { FadeTime = Math.Min( start.PeakTime, start.FadeTime - delta ) } : null,
			End is { } end ? end with { FadeTime = Math.Max( end.PeakTime, end.FadeTime + delta ) } : null );
	}

	public TimeSelection WithPeak( float time, InterpolationMode defaultInterpolation )
	{
		return new TimeSelection(
			Start is { } start
				? start with { PeakTime = time, FadeTime = time - start.Duration }
				: new FadeSelection( time, time, defaultInterpolation ),
			End is { } end
				? end with { PeakTime = time, FadeTime = time + end.Duration }
				: new FadeSelection( time, time, defaultInterpolation ) );
	}

	public TimeSelection WithPeakStart( float time, InterpolationMode defaultInterpolation, bool keepDuration = true )
	{
		if ( Start is not { } start ) return this with { Start = new FadeSelection( time, time, defaultInterpolation ) };

		time = Math.Min( time, End?.PeakTime ?? float.PositiveInfinity );

		return this with { Start = start with
		{
			PeakTime = time,
			FadeTime = keepDuration ? time - start.Duration : start.FadeTime
		} };
	}

	public TimeSelection WithPeakEnd( float time, InterpolationMode defaultInterpolation, bool keepDuration = true )
	{
		if ( End is not { } end ) return this with { End = new FadeSelection( time, time, defaultInterpolation ) };

		time = Math.Max( time, Start?.PeakTime ?? 0f );

		return this with
		{
			End = end with
			{
				PeakTime = time,
				FadeTime = keepDuration ? time + end.Duration : end.FadeTime
			}
		};
	}

	public TimeSelection WithFadeStart( float time )
	{
		if ( Start is not { } start ) return this;

		return this with { Start = start with { FadeTime = Math.Min( start.PeakTime, time ) } };
	}

	public TimeSelection WithFadeEnd( float time )
	{
		if ( End is not { } end ) return this;

		return this with { End = end with { FadeTime = Math.Max( end.PeakTime, time ) } };
	}

	public bool Overlaps( float minTime, float maxTime )
	{
		if ( Start is { } start && start.FadeTime > maxTime ) return false;
		if ( End is { } end && end.FadeTime < minTime ) return false;

		return true;
	}

	public float GetFadeValue( float time )
	{
		if ( Start is { } start && time < start.PeakTime )
		{
			return time > start.FadeTime
				? start.Interpolation.Apply( (time - start.FadeTime) / start.Duration )
				: 0f;
		}

		if ( End is { } end && time > end.PeakTime )
		{
			return time < end.FadeTime
				? end.Interpolation.Apply( (end.FadeTime - time) / end.Duration )
				: 0f;
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

			if ( value.Start is { } start )
			{
				var time = EditMode.Session.PixelsToTime( Position.x, true );

				value = value with { Start = start with { PeakTime = time, FadeTime = time - start.Duration } };
			}

			if ( value.End is { } end )
			{
				var time = EditMode.Session.PixelsToTime( Position.x + Size.x, true );

				value = value with { End = end with { PeakTime = time, FadeTime = time + end.Duration } };
			}

			EditMode.TimeSelection = value;
		}

		public void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			var startTime = value.Start?.PeakTime ?? 0f;
			var endTime = value.End?.PeakTime ?? EditMode.Session.Clip?.Duration ?? 0f;

			Position = new Vector2( EditMode.Session.TimeToPixels( startTime ), viewRect.Top );
			Size = new Vector2( EditMode.Session.TimeToPixels( endTime - startTime ), viewRect.Height );

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

		public FadeSelection? Value
		{
			get => Kind == FadeKind.FadeIn ? EditMode.TimeSelection?.Start : EditMode.TimeSelection?.End;
			set
			{
				if ( EditMode.TimeSelection is not { } selection ) return;

				if ( Kind == FadeKind.FadeIn )
				{
					EditMode.TimeSelection = selection with { Start = value };
				}
				else
				{
					EditMode.TimeSelection = selection with { End = value };
				}
			}
		}

		public int Sign => Kind == FadeKind.FadeIn ? 1 : -1;

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
				Position = new Vector2( EditMode.Session.TimeToPixels( fade.PeakTime - Sign * fade.Duration ), viewRect.Top );
				Size = new Vector2( EditMode.Session.TimeToPixels( fade.Duration ), viewRect.Height );
			}

			Update();
		}

		protected override void OnMoved()
		{
			if ( EditMode.TimeSelection is not { } selection || Value is not { } value ) return;

			if ( selection is { Start: { } start, End: { } end } && start.PeakTime >= end.PeakTime - 0.01f )
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
					FadeKind.FadeIn => selection.WithPeakStart( time + Sign * value.Duration, value.Interpolation ),
					FadeKind.FadeOut => selection.WithPeakEnd( time + Sign * value.Duration, value.Interpolation ),
					_ => selection
				};
			}
		}

		protected override void OnKeyPress( KeyEvent e )
		{
			Log.Info( $"Key press: {e.Key}" );

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

		private readonly Func<TimeSelection, float?> _getTime;
		private readonly Func<TimeSelection, float, TimeSelection> _setTime;

		public TimeSelectionHandleItem( MotionEditMode editMode, Func<TimeSelection, float?> getTime, Func<TimeSelection, float, TimeSelection> setTime )
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
