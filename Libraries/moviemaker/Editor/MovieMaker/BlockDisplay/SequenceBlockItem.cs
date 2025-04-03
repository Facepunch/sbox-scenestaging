using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public sealed class SequenceBlockItem : BlockItem<ProjectSequenceBlock>
{
	private RealTimeSince _lastClick;

	private enum EditMode
	{
		None,
		Translate,
		MoveStart,
		MoveEnd,
		Split
	}

	private EditMode _editMode;
	private float _dragOffset;
	private MovieTimeRange _originalTimeRange;
	private MovieTransform _originalTransform;

	private GraphicsItem? _ghost;

	public new ProjectSequenceTrack Track => (ProjectSequenceTrack)Parent.View.Track;

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
		if ( Parent.View.IsLocked ) return;
		if ( !e.LeftMouseButton && !e.RightMouseButton ) return;

		e.Accepted = true;

		var time = Parent.Session.ScenePositionToTime( e.ScenePosition );

		if ( e.RightMouseButton )
		{
			Parent.Session.SetCurrentPointer( time );
		}

		if ( !e.LeftMouseButton ) return;

		if ( !e.HasShift )
		{
			Parent.DopeSheet.DeselectAll();
		}

		Selected = true;

		_editMode = GetEditMode( e.LocalPosition );
		_dragOffset = e.ScenePosition.x - SceneRect.Left;
		_originalTimeRange = Block.TimeRange;
		_originalTransform = Block.Transform;

		if ( _editMode == EditMode.Split )
		{
			OnSplit( time );
			_editMode = EditMode.None;
			return;
		}

		var fullSceneRect = FullSceneRect;

		_ghost = new FullBlockGhostItem();
		_ghost.Position = new Vector2( fullSceneRect.Left, Position.y );
		_ghost.Size = new Vector2( fullSceneRect.Width, Height );
		_ghost.Parent = Parent;
	}

	private MovieTimeRange FullTimeRange
	{
		get
		{
			var sourceTimeRange = new MovieTimeRange( 0d, Block.Resource.GetCompiled().Duration );
			return new MovieTimeRange( Block.Transform * sourceTimeRange.Start, Block.Transform * sourceTimeRange.End );
		}
	}

	public Rect FullSceneRect
	{
		get
		{
			var fullTimeRange = FullTimeRange.ClampStart( 0d );

			var min = Parent.Session.TimeToPixels( fullTimeRange.Start );
			var max = Parent.Session.TimeToPixels( fullTimeRange.End );

			return SceneRect with { Left = min, Right = max };
		}
	}

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		if ( _editMode == EditMode.None ) return;

		e.Accepted = true;

		// To avoid double-click
		_lastClick = 1f;

		switch ( _editMode )
		{
			case EditMode.MoveStart or EditMode.MoveEnd:
				OnDragStartEnd( e.ScenePosition );
				break;

			case EditMode.Translate:
				OnTranslate( e.ScenePosition );
				break;
		}

		Layout();

		Parent.View.MarkValueChanged();
		Parent.Session.ApplyFrame( Parent.View.Track, Parent.Session.CurrentPointer );
	}

	private void OnSplit( MovieTime time )
	{
		if ( time <= TimeRange.Start ) return;
		if ( time >= TimeRange.End ) return;

		Track.AddBlock( (time, Block.TimeRange.End), Block.Transform, Block.Resource );

		Block.TimeRange = (Block.TimeRange.Start, time);

		Layout();
		Parent.View.MarkValueChanged();
	}

	private void OnDragStartEnd( Vector2 scenePosition )
	{
		var time = Parent.Session.ScenePositionToTime( scenePosition, new SnapOptions( IgnoreBlock: Block ) );
		var minDuration = MovieTime.FromFrames( 1, Parent.Session.FrameRate );

		Block.TimeRange = FullTimeRange.Clamp( _editMode switch
		{
			EditMode.MoveStart => (MovieTime.Min( time, _originalTimeRange.End - minDuration ), _originalTimeRange.End),
			EditMode.MoveEnd => (_originalTimeRange.Start, MovieTime.Max( time, _originalTimeRange.Start + minDuration )),
			_ => null
		} );
	}

	private void OnTranslate( Vector2 scenePosition )
	{
		scenePosition.x -= _dragOffset;

		var fullTimeRange = FullTimeRange;
		var time = Parent.Session.ScenePositionToTime( scenePosition,
			new SnapOptions( IgnoreBlock: Block, SnapOffsets: [TimeRange.Duration, fullTimeRange.Start - TimeRange.Start, fullTimeRange.End - TimeRange.End] ) );

		var difference = time - _originalTimeRange.Start;

		Block.TimeRange = _originalTimeRange + difference;
		Block.Transform = _originalTransform + difference;

		if ( _ghost is not { } ghost ) return;

		ghost.PrepareGeometryChange();

		var fullSceneRect = FullSceneRect;

		_ghost.Position = new Vector2( FullSceneRect.Left, Position.y );
		_ghost.Width = fullSceneRect.Width;
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		if ( e.RightMouseButton )
		{
			OnOpenContextMenu( e );
			return;
		}

		if ( !e.LeftMouseButton ) return;

		if ( _lastClick < 0.5f )
		{
			OnEdit();
		}

		_lastClick = 0f;
		_editMode = EditMode.None;
		_ghost?.Destroy();
		_ghost = null;

		e.Accepted = true;

		Layout();
	}

	private void OnOpenContextMenu( GraphicsMouseEvent e )
	{
		e.Accepted = true;

		Selected = true;

		var time = Parent.Session.CurrentPointer;

		var menu = new Menu();

		menu.AddHeading( "Sequence Block" );

		menu.AddOption( "Edit", "edit", OnEdit );

		if ( time > TimeRange.Start && time < TimeRange.End )
		{
			menu.AddOption( "Split", "carpenter", () => OnSplit( time ) );
		}

		menu.AddOption( "Delete", "delete", OnDelete );

		menu.OpenAt( e.ScreenPosition );
	}

	private void OnDelete()
	{
		Track.RemoveBlock( Block );
		Parent.View.MarkValueChanged();
	}

	private void OnEdit()
	{
		if ( Block.Resource is { } resource )
		{
			Parent.Session.Editor.EnterSequence( resource, Block.Transform, Block.Transform.Inverse * Block.TimeRange );
		}
	}

	private EditMode GetEditMode( Vector2 localMousePos )
	{
		if ( localMousePos.x <= 8f ) return EditMode.MoveStart;
		if ( localMousePos.x >= LocalRect.Width - 8f ) return EditMode.MoveEnd;
		// if ( (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0 ) return EditMode.Split;

		return EditMode.Translate;
	}

	private void UpdateCursor( Vector2 localMousePos )
	{
		if ( Parent.View.IsLocked )
		{
			Cursor = CursorShape.Arrow;
			return;
		}

		Cursor = GetEditMode( localMousePos ) switch
		{
			EditMode.MoveStart or EditMode.MoveEnd => CursorShape.SizeH,
			EditMode.Split => CursorShape.SplitH,
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

		var textRect = new Rect( minX + 4f, LocalRect.Top + 4f, maxX - minX - 8f, LocalRect.Height - 4f );
		var fullTimeRange = FullTimeRange;

		if ( _editMode == EditMode.MoveEnd )
		{
			TryDrawText( ref textRect, $"{Block.TimeRange.End - fullTimeRange.Start}", TextFlag.RightCenter );
			TryDrawText( ref textRect, $"{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
		}
		else if ( _editMode != EditMode.None )
		{
			TryDrawText( ref textRect, $"{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
			TryDrawText( ref textRect, $"{Block.TimeRange.End - fullTimeRange.Start}", TextFlag.RightCenter );
		}

		TryDrawText( ref textRect, Block.Resource.ResourceName.ToTitleCase(), icon: "movie" );
	}

	private void TryDrawText( ref Rect rect, string text, TextFlag flags = TextFlag.Center, string? icon = null, float iconSize = 16f )
	{
		var originalRect = rect;

		if ( icon != null )
		{
			if ( rect.Width < iconSize ) return;

			rect.Left += iconSize + 4f;
		}

		var textRect = Paint.MeasureText( rect, text, flags );

		if ( textRect.Width > rect.Width )
		{
			if ( icon != null )
			{
				Paint.DrawIcon( originalRect, icon, iconSize, flags );
			}

			rect = default;
			return;
		}

		if ( icon != null )
		{
			Paint.DrawIcon( new Rect( textRect.Left - iconSize - 4f, rect.Top, iconSize, rect.Height ), icon, iconSize, flags );
		}

		Paint.DrawText( rect, text, flags );

		if ( (flags & TextFlag.Left) != 0 )
		{
			rect.Left = textRect.Right;
		}
		else if ( (flags & TextFlag.Right) != 0 )
		{
			rect.Right = textRect.Left;
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
