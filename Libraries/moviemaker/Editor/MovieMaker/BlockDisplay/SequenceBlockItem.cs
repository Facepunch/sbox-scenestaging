using System.Collections.Immutable;
using System.Linq;
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
	private bool _dragged;
	private MovieTimeRange _originalTimeRange;
	private MovieTransform _originalTransform;

	private GraphicsItem? _ghost;
	private IHistoryScope? _historyScope;

	public new ProjectSequenceTrack Track => (ProjectSequenceTrack)Parent.View.Track;

	public string BlockTitle => Block.Resource.ResourceName.ToTitleCase();

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

	private readonly HashSet<SequenceBlockItem> _draggedItems = new();
	private readonly HashSet<ITrackBlock> _draggedBlocks = new();

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		_dragged = false;

		if ( Parent.View.IsLocked ) return;
		if ( !e.LeftMouseButton && !e.RightMouseButton ) return;

		// e.Accepted = true;

		var time = Parent.Session.ScenePositionToTime( e.ScenePosition );

		if ( e.RightMouseButton )
		{
			Parent.Session.PlayheadTime = time;
		}

		if ( !e.LeftMouseButton ) return;

		Selected = true;

		_draggedItems.Clear();
		_draggedBlocks.Clear();

		foreach ( var item in Parent.Timeline.SelectedItems.OfType<SequenceBlockItem>() )
		{
			_draggedItems.Add( item );
			_draggedBlocks.Add( item.Block );
		}

		foreach ( var item in _draggedItems )
		{
			item.OnDragStart( e, time );
		}

		var fullSceneRect = FullSceneRect;

		_ghost = new FullBlockGhostItem();
		_ghost.Position = new Vector2( fullSceneRect.Left, Position.y );
		_ghost.Size = new Vector2( fullSceneRect.Width, Height );
		_ghost.Parent = Parent;
	}

	private void OnDragStart( GraphicsMouseEvent e, MovieTime time )
	{
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

		switch ( _editMode )
		{
			case EditMode.MoveStart or EditMode.MoveEnd:
				var end = _editMode is EditMode.MoveStart ? "Start" : "End";
				_historyScope ??= Parent.Session.History.Push( $"Move Sequence {end} ({BlockTitle})" );
				break;

			case EditMode.Translate:
				_historyScope ??= Parent.Session.History.Push( $"Move Sequence ({BlockTitle})" );
				break;
		}

		foreach ( var item in _draggedItems )
		{
			item.OnDrag( e, _draggedBlocks );
		}

		foreach ( var trackView in _draggedItems.Select( x => x.Parent.View ).Distinct() )
		{
			trackView.MarkValueChanged();
			trackView.ApplyFrame( Parent.Session.PlayheadTime );
		}
	}

	private void OnDrag( GraphicsMouseEvent e, IReadOnlySet<ITrackBlock> snapIgnore )
	{
		_dragged = true;

		switch ( _editMode )
		{
			case EditMode.MoveStart or EditMode.MoveEnd:
				OnDragStartEnd( e.ScenePosition, snapIgnore );
				break;

			case EditMode.Translate:
				OnTranslate( e.ScenePosition, snapIgnore );
				break;
		}

		Layout();
	}

	private void OnSplit( MovieTime time )
	{
		if ( time <= TimeRange.Start ) return;
		if ( time >= TimeRange.End ) return;


		using ( Parent.Session.History.Push( $"Split Sequence ({BlockTitle})" ) )
		{
			Track.AddBlock( (time, Block.TimeRange.End), Block.Transform, Block.Resource );

			Block.TimeRange = (Block.TimeRange.Start, time);
		}

		Layout();
		Parent.View.MarkValueChanged();
	}

	private void OnDragStartEnd( Vector2 scenePosition, IReadOnlySet<ITrackBlock> snapIgnore )
	{
		var time = Parent.Session.ScenePositionToTime( scenePosition, new SnapOptions( IgnoreBlocks: snapIgnore ) );
		var minDuration = MovieTime.FromFrames( 1, Parent.Session.FrameRate );

		Block.TimeRange = FullTimeRange.Clamp( _editMode switch
		{
			EditMode.MoveStart => (MovieTime.Min( time, _originalTimeRange.End - minDuration ), _originalTimeRange.End),
			EditMode.MoveEnd => (_originalTimeRange.Start, MovieTime.Max( time, _originalTimeRange.Start + minDuration )),
			_ => null
		} );
	}

	private void OnTranslate( Vector2 scenePosition, IReadOnlySet<ITrackBlock> snapIgnore )
	{
		scenePosition.x -= _dragOffset;

		var fullTimeRange = FullTimeRange;
		var time = Parent.Session.ScenePositionToTime( scenePosition,
			new SnapOptions( IgnoreBlocks: snapIgnore, SnapOffsets: [TimeRange.Duration, fullTimeRange.Start - TimeRange.Start, fullTimeRange.End - TimeRange.End] ) );

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

		if ( !_dragged && _lastClick < 0.5f )
		{
			OnEdit();
		}

		_historyScope?.Dispose();
		_historyScope = null;

		_lastClick = 0f;
		_editMode = EditMode.None;
		_ghost?.Destroy();
		_ghost = null;

		Layout();
	}

	private void OnOpenContextMenu( GraphicsMouseEvent e )
	{
		e.Accepted = true;

		Selected = true;

		var time = Parent.Session.PlayheadTime;

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
		using ( Parent.Session.History.Push( "Sequence Deleted" ) )
		{
			Track.RemoveBlock( Block );

			if ( Track.IsEmpty )
			{
				Parent.View.Remove();
			}
		}

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

		var color = Theme.Primary.Desaturate( isLocked ? 0.25f : 0f ).Darken( isLocked ? 0.5f : isSelected ? 0f : isHovered ? 0.1f : 0.25f );

		PaintExtensions.PaintFilmStrip( LocalRect.Shrink( 0f, 0f, 1f, 0f ), color );

		var minX = LocalRect.Left;
		var maxX = LocalRect.Right;

		Paint.ClearBrush();
		Paint.SetPen( Theme.TextControl.Darken( isLocked ? 0.25f : 0f ) );

		var textRect = new Rect( minX + 8f, LocalRect.Top, maxX - minX - 16f, LocalRect.Height );
		var fullTimeRange = FullTimeRange;

		if ( _editMode == EditMode.MoveEnd )
		{
			TryDrawText( ref textRect, $"{Block.TimeRange.End - fullTimeRange.Start}", TextFlag.RightCenter );
			TryDrawText( ref textRect, $"{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
		}
		else if ( _editMode == EditMode.MoveStart )
		{
			TryDrawText( ref textRect, $"{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
			TryDrawText( ref textRect, $"{Block.TimeRange.End - fullTimeRange.Start}", TextFlag.RightCenter );
		}
		else
		{
			TryDrawText( ref textRect, BlockTitle, icon: "movie", flags: TextFlag.LeftCenter );
		}
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
		Paint.SetBrushAndPen( Timeline.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect, 2 );
	}
}
