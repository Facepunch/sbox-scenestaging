using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public sealed class SequenceBlockItem : BlockItem<ProjectSequenceBlock>
{
	private RealTimeSince _lastClick;

	private enum DragMode
	{
		None,
		Translate,
		MoveStart,
		MoveEnd
	}

	public override Rect BoundingRect => base.BoundingRect.Grow( 8f, 0f );

	private DragMode _dragMode;
	private MovieTime _dragStartTime;
	private MovieTimeRange _originalTimeRange;
	private MovieTransform _originalTransform;

	private GraphicsItem? _ghost;

	public SequenceBlockItem()
	{
		HoverEvents = true;
		Selectable = true;

		Cursor = CursorShape.Finger;
	}

	protected override void OnDestroy()
	{
		_ghost?.Destroy();
		_ghost = null;
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		if ( Parent.View.IsLocked || !e.LeftMouseButton ) return;

		e.Accepted = true;

		Selected = true;

		_dragMode = GetDragMode( e.LocalPosition );
		_dragStartTime = Parent.Session.ScenePositionToTime( e.ScenePosition, SnapFlag.TrackBlock );
		_originalTimeRange = Block.TimeRange;
		_originalTransform = Block.Transform;

		if ( _dragMode is DragMode.MoveStart or DragMode.MoveEnd )
		{
			var fullSceneRect = FullSceneRect;

			_ghost = new FullBlockGhostItem();
			_ghost.Position = new Vector2( fullSceneRect.Left, Position.y );
			_ghost.Size = new Vector2( fullSceneRect.Width, Height );
			_ghost.Parent = Parent;
		}
	}

	private MovieTimeRange FullTimeRange
	{
		get
		{
			var sourceTimeRange = new MovieTimeRange( 0d, Block.Resource.GetCompiled().Duration );
			return new MovieTimeRange( _originalTransform * sourceTimeRange.Start, _originalTransform * sourceTimeRange.End ).ClampStart( 0d );
		}
	}

	public Rect FullSceneRect
	{
		get
		{
			var fullTimeRange = FullTimeRange;

			var min = Parent.Session.TimeToPixels( fullTimeRange.Start );
			var max = Parent.Session.TimeToPixels( fullTimeRange.End );

			return SceneRect with { Left = min, Right = max };
		}
	}

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		if ( _dragMode == DragMode.None ) return;

		e.Accepted = true;

		// To avoid double-click
		_lastClick = 1f;

		var time = Parent.Session.ScenePositionToTime( e.ScenePosition, ignoreTrack: Parent.View );
		var minDuration = MovieTime.FromFrames( 1, Parent.Session.FrameRate );
		var fullTimeRange = FullTimeRange;

		switch ( _dragMode )
		{
			case DragMode.MoveStart:
				Block.TimeRange = fullTimeRange.Clamp( (MovieTime.Min( time, _originalTimeRange.End - minDuration ), _originalTimeRange.End) );
				break;

			case DragMode.MoveEnd:
				Block.TimeRange = fullTimeRange.Clamp( (_originalTimeRange.Start, MovieTime.Max( time, _originalTimeRange.Start + minDuration )) );
				break;

			case DragMode.Translate:
				var difference = MovieTime.Max( time - _dragStartTime, -_originalTimeRange.Start );
				Block.TimeRange = _originalTimeRange + difference;
				Block.Transform = _originalTransform + difference;
				break;
		}

		Layout();

		Parent.View.MarkValueChanged();
		Parent.Session.ApplyFrame( Parent.View.Track, Parent.Session.CurrentPointer );
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		if ( !e.LeftMouseButton ) return;

		if ( _lastClick < 0.5f )
		{
			DoubleClicked();
		}

		_lastClick = 0f;
		_dragMode = DragMode.None;
		_ghost?.Destroy();
		_ghost = null;

		e.Accepted = true;

		Layout();
	}

	private void DoubleClicked()
	{
		if ( Block.Resource is { } resource )
		{
			Parent.Session.Editor.EnterSequence( resource );
		}
	}

	private DragMode GetDragMode( Vector2 localMousePos )
	{
		if ( localMousePos.x <= 8f ) return DragMode.MoveStart;
		if ( localMousePos.x >= LocalRect.Width - 8f ) return DragMode.MoveEnd;

		return DragMode.Translate;
	}

	private void UpdateCursor( Vector2 localMousePos )
	{
		if ( Parent.View.IsLocked )
		{
			Cursor = CursorShape.Arrow;
			return;
		}

		Cursor = GetDragMode( localMousePos ) switch
		{
			DragMode.MoveStart or DragMode.MoveEnd => CursorShape.SizeH,
			_ => CursorShape.Finger
		};
	}

	protected override void OnHoverEnter( GraphicsHoverEvent e )
	{
		base.OnHoverEnter( e );
		UpdateCursor( e.LocalPosition );
	}

	protected override void OnHoverMove( GraphicsHoverEvent e )
	{
		base.OnHoverMove( e );
		UpdateCursor( e.LocalPosition );
	}

	protected override void OnPaint()
	{
		var isLocked = Parent.View.IsLocked;
		var isSelected = !isLocked && Selected;
		var isHovered = !isLocked && Paint.HasMouseOver;

		Paint.SetBrushAndPen( Theme.Primary.Desaturate( isLocked ? 0.25f : 0f ).Darken( isLocked ? 0.5f : isSelected ? 0f : isHovered ? 0.1f : 0.25f ) );
		Paint.DrawRect( LocalRect, 2 );

		var viewMin = FromScene( Parent.GraphicsView.SceneRect.TopLeft );
		var viewMax = FromScene( Parent.GraphicsView.SceneRect.BottomRight );
		var minX = Math.Max( LocalRect.Left, viewMin.x );
		var maxX = Math.Min( LocalRect.Right, viewMax.x );

		Paint.SetBrush( Theme.ControlBackground );

		for ( var x = minX.SnapToGrid( 8f ); x <= maxX; x += 8f )
		{
			Paint.DrawRect( new Rect( x - 2f, LocalRect.Top + 2f, 4f, 6f ) );
		}

		Paint.ClearBrush();
		Paint.SetPen( Theme.ControlText.Darken( isLocked ? 0.25f : 0f ) );

		var textRect = new Rect( minX + 8f, LocalRect.Top + 4f, maxX - minX - 16f, LocalRect.Height - 4f );
		var fullTimeRange = FullTimeRange;

		switch ( _dragMode )
		{
			case DragMode.MoveStart or DragMode.MoveEnd:
				Paint.DrawText( textRect, $"+{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
				Paint.DrawText( textRect, $"{Block.TimeRange.End - fullTimeRange.End}", TextFlag.RightCenter );
				break;

			default:
				Paint.DrawText( textRect, Block.Resource.ResourcePath, TextFlag.Center );
				break;
		}
	}
}

file sealed class FullBlockGhostItem : GraphicsItem
{
	public FullBlockGhostItem()
	{
		ZIndex = -100;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( DopeSheet.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect, 2 );
	}
}
