using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private abstract class TimeSelectionItem : GraphicsItem
	{
		/// <summary>
		/// Capture time selection before being dragged so we can revert etc.
		/// </summary>
		protected TimeSelection? OriginalSelection { get; private set; }
		protected IModificationOptions? OriginalModificationOptions { get; private set; }

		public MotionEditMode EditMode { get; }

		protected TimeSelectionItem( MotionEditMode editMode )
		{
			EditMode = editMode;
		}

		public abstract void UpdatePosition( TimeSelection value, Rect viewRect );

		protected override void OnMousePressed( GraphicsMouseEvent e )
		{
			base.OnMousePressed( e );

			OriginalSelection = EditMode.TimeSelection;
			OriginalModificationOptions = EditMode.Modification?.Options;
		}

		protected override void OnMouseReleased( GraphicsMouseEvent e )
		{
			base.OnMouseReleased( e );

			OriginalSelection = null;
		}
	}

	/// <summary>
	/// Inner region of the timeline selection. Dragging it moves the whole selection left / right.
	/// </summary>
	private sealed class TimeSelectionPeakItem : TimeSelectionItem
	{
		public TimeSelectionPeakItem( MotionEditMode editMode )
			: base( editMode )
		{
			ZIndex = 10000;

			Movable = true;
			Cursor = CursorShape.Finger;
		}

		protected override void OnMoved()
		{
			if ( OriginalSelection is not { } selection ) return;

			var origTime = selection.PeakStart;
			var startTime = EditMode.Session.ScenePositionToTime( Position, ignore: SnapFlag.Selection | SnapFlag.PasteBlock, null,
				selection.TotalStart - origTime, selection.PeakEnd - origTime, selection.TotalEnd - origTime );

			startTime = MovieTime.Max( selection.FadeIn.Duration, startTime );

			if ( OriginalModificationOptions is ITranslatableOptions translatable )
			{
				EditMode.Modification!.Options = translatable.WithOffset( startTime + translatable.Offset - selection.PeakStart );
			}

			EditMode.TimeSelection = selection with { PeakTimeRange = (startTime, startTime + selection.PeakTimeRange.Duration) };
		}

		public override void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			var timeRange = value.PeakTimeRange;

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

			if ( EditMode.LastActionIcon is { } icon && EditMode._lastActionTime < 1f )
			{
				var t = 1f - EditMode._lastActionTime;

				Paint.SetPen( Color.White.WithAlpha( t * t * t * t ) );
				Paint.DrawIcon( LocalRect.Grow( 32f, 0f ), icon, 32f );
			}
		}
	}

	private enum FadeKind
	{
		FadeIn,
		FadeOut
	}

	/// <summary>
	/// Fade in / out region of the timeline selection. Dragging it moves the fade left / right.
	/// If the selection has a zero-width peak (it fades out right after fading in), then
	/// you can move the whole selection by starting a drag in the direction of the other
	/// fade item.
	/// </summary>
	private sealed class TimeSelectionFadeItem : TimeSelectionItem
	{
		private bool? _moveWholeSelection;

		public FadeKind Kind { get; }

		public MovieTimeRange? TimeRange
		{
			get => Kind == FadeKind.FadeIn
				? EditMode.TimeSelection?.FadeInTimeRange
				: EditMode.TimeSelection?.FadeOutTimeRange;
		}

		public InterpolationMode? Interpolation
		{
			get => Kind == FadeKind.FadeIn
				? EditMode.TimeSelection?.FadeIn.Interpolation
				: EditMode.TimeSelection?.FadeOut.Interpolation;

			set
			{
				if ( EditMode.TimeSelection is not { } selection ) return;
				if ( value is not { } mode ) return;

				EditMode.TimeSelection = Kind == FadeKind.FadeIn
					? selection with { FadeIn = selection.FadeIn with { Interpolation = mode } }
					: selection with { FadeOut = selection.FadeOut with { Interpolation = mode } };
			}
		}

		public TimeSelectionFadeItem( MotionEditMode editMode, FadeKind kind )
			: base( editMode )
		{
			Kind = kind;

			ZIndex = 10001;

			HandlePosition = kind == FadeKind.FadeIn ? new Vector2( 1f, 0f ) : new Vector2( 0f, 0f );

			Movable = true;
			HoverEvents = true;
			Focusable = true;

			Cursor = CursorShape.Finger;
		}

		protected override void OnMousePressed( GraphicsMouseEvent e )
		{
			base.OnMousePressed( e );

			if ( OriginalSelection is not { } value ) return;

			_moveWholeSelection = value.PeakTimeRange.IsEmpty ? null : false;
		}

		public override void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			if ( TimeRange is not { } timeRange )
			{
				Position = new Vector2( -50000f, 0f );
			}
			else
			{
				Position = new Vector2( EditMode.Session.TimeToPixels( Kind == FadeKind.FadeIn ? timeRange.End : timeRange.Start ), viewRect.Top );
				Size = new Vector2( EditMode.Session.TimeToPixels( timeRange.Duration ), viewRect.Height );
			}

			Update();
		}

		protected override void OnMoved()
		{
			if ( OriginalSelection is not { } selection ) return;

			var time = _moveWholeSelection is true
				? EditMode.Session.ScenePositionToTime( Position, ignore: SnapFlag.Selection, null,
					-selection.FadeIn.Duration, selection.FadeOut.Duration )
				: EditMode.Session.ScenePositionToTime( Position,
					ignore: Kind == FadeKind.FadeIn ? SnapFlag.SelectionStart : SnapFlag.SelectionEnd, null,
					Kind == FadeKind.FadeIn ? -selection.FadeIn.Duration : selection.FadeOut.Duration );

			if ( time != selection.PeakStart )
			{
				_moveWholeSelection ??= Kind == FadeKind.FadeIn
					? time > selection.PeakStart
					: time < selection.PeakStart;
			}

			if ( _moveWholeSelection is true )
			{
				time = MovieTime.Max( selection.FadeIn.Duration, time );

				if ( OriginalModificationOptions is ITranslatableOptions translatable )
				{
					EditMode.Modification!.Options = translatable.WithOffset( time + translatable.Offset - selection.PeakStart );
				}

				EditMode.TimeSelection = selection.WithTimes(
					totalStart: time - selection.FadeIn.Duration, peakStart: time,
					peakEnd: time, totalEnd: time + selection.FadeOut.Duration );
			}
			else if ( Kind == FadeKind.FadeIn )
			{
				time = time.Clamp( (selection.FadeIn.Duration, selection.PeakEnd) );

				EditMode.TimeSelection = selection.WithTimes( totalStart: time - selection.FadeIn.Duration, peakStart: time );
			}
			else
			{
				time = MovieTime.Max( selection.PeakStart, time );

				EditMode.TimeSelection = selection.WithTimes( peakEnd: time, totalEnd: time + selection.FadeOut.Duration );
			}
		}

		private readonly List<Vector2> _points = new();

		protected override void OnPaint()
		{
			if ( Width < 1f || Interpolation is not { } interpolation ) return;

			var color = EditMode.SelectionColor;

			if ( Hovered ) color = color.Lighten( 0.1f );

			var fadeColor = color.WithAlpha( 0.02f );
			var scrubBarHeight = EditMode.DopeSheet.ScrubBarTop.Height;

			var (x0, x1) = Kind == FadeKind.FadeIn
				? (0f, Width)
				: (Width, 0f);

			_points.Clear();

			AddPoints( _points, interpolation, new Vector2( x0, LocalRect.Top + scrubBarHeight ), new Vector2( x1, LocalRect.Top ), false );
			AddPoints( _points, interpolation, new Vector2( x1, LocalRect.Bottom ), new Vector2( x0, LocalRect.Bottom - scrubBarHeight ), true );

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

	/// <summary>
	/// One of the boundaries between the 3 parts of the selection (fade in / peak / fade out). Drag it to move
	/// just that boundary. If sections of the selection are zero-width (no peak / no fade in / no fade out),
	/// then we work out which handle you wanted to drag based on the direction the drag starts. You can't move
	/// these handles past one another.
	/// </summary>
	private sealed class TimeSelectionHandleItem : TimeSelectionItem
	{
		private enum Index
		{
			TotalStart,
			PeakStart,
			PeakEnd,
			TotalEnd
		}

		private Index _minIndex;
		private Index _maxIndex;

		public TimeSelectionHandleItem( MotionEditMode editMode )
			: base( editMode )
		{
			Movable = true;
			HoverEvents = true;

			Cursor = CursorShape.SizeH;
			HandlePosition = new Vector2( 0.5f, 0f );

			ZIndex = 10002;
		}

		protected override void OnMousePressed( GraphicsMouseEvent e )
		{
			base.OnMousePressed( e );

			(_minIndex, _maxIndex) = GetIndexRange();
		}

		protected override void OnMoved()
		{
			var ignore = (SnapFlag)((int)SnapFlag.SelectionTotalStart << (int)_minIndex);
			var time = EditMode.Session.ScenePositionToTime( Position, ignore: ignore );

			if ( OriginalSelection is not { } value ) return;

			var originTime = GetTime( value, _minIndex );

			// If it's ambiguous which handle we are, pick a side
			// based on which direction we're dragged

			if ( time < originTime ) _maxIndex = _minIndex;
			if ( time > originTime ) _minIndex = _maxIndex;

			// Limit dragging to neighbouring control points

			var minTime = GetTime( value, _minIndex - 1 );
			var maxTime = GetTime( value, _maxIndex + 1 );

			EditMode.TimeSelection = SetTime( value, _minIndex, time.Clamp( (minTime, maxTime) ) );
		}

		private static MovieTime GetTime( TimeSelection value, Index index ) => index switch
		{
			Index.TotalStart => value.TotalStart,
			Index.PeakStart => value.PeakStart,
			Index.PeakEnd => value.PeakEnd,
			Index.TotalEnd => value.TotalEnd,
			< 0 => MovieTime.Zero,
			> Index.TotalEnd => MovieTime.MaxValue
		};

		private static TimeSelection SetTime( TimeSelection value, Index index, MovieTime time ) => index switch
		{
			Index.TotalStart => value.WithTimes( totalStart: time ),
			Index.PeakStart => value.WithTimes( peakStart: time ),
			Index.PeakEnd => value.WithTimes( peakEnd: time ),
			Index.TotalEnd => value.WithTimes( totalEnd: time ),
			_ => value
		};

		public override void UpdatePosition( TimeSelection value, Rect viewRect )
		{
			PrepareGeometryChange();

			var time = GetTime( value, GetIndexRange().Min );

			Position = new Vector2( EditMode.Session.TimeToPixels( time ), viewRect.Top );
			Size = new Vector2( 8f, viewRect.Height );
		}

		/// <summary>
		/// Get the possible range of handles this could be, if the time selection has
		/// overlapping control points.
		/// </summary>
		private (Index Min, Index Max) GetIndexRange()
		{
			if ( EditMode.TimeSelection is not { } value ) return default;

			var handles = EditMode.DopeSheet.Items.OfType<TimeSelectionHandleItem>()
				.OrderBy( x => x.Position.x );

			var index = Index.TotalStart + handles.TakeWhile( x => x != this ).Count();

			var minIndex = index;
			var maxIndex = index;

			var time = GetTime( value, index );

			while ( minIndex > Index.TotalStart && GetTime( value, minIndex - 1 ) == time )
			{
				minIndex -= 1;
			}

			while ( maxIndex < Index.TotalEnd && GetTime( value, maxIndex + 1 ) == time )
			{
				maxIndex += 1;
			}

			return (minIndex, maxIndex);
		}
	}
}
