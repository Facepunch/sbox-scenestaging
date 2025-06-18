using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public class Timeline : GraphicsView
{
	public const float TrackHeight = 32f;
	public const float RootTrackSpacing = 8f;

	public static class Colors
	{
		public static Color Background => Theme.WidgetBackground;
		public static Color ChannelBackground => Theme.ControlBackground;
		public static Color HandleSelected => Color.White;
	}

	public Session Session { get; }

	private readonly BackgroundItem _backgroundItem;
	private readonly GridItem _gridItem;
	private readonly SynchronizedSet<TrackView, TimelineTrack> _tracks;

	private readonly CurrentPointerItem _currentPointerItem;
	private readonly CurrentPointerItem _previewPointerItem;

	public ScrubberItem ScrubBarTop { get; }
	public ScrubberItem ScrubBarBottom { get; }

	public IEnumerable<TimelineTrack> Tracks => _tracks;

	public Rect VisibleRect
	{
		get
		{
			var screenRect = ScreenRect;
			var topLeft = FromScreen( screenRect.TopLeft );
			var bottomRight = FromScreen( screenRect.BottomRight );

			return ToScene( new Rect( topLeft, bottomRight - topLeft ) );
		}
	}

	public Timeline( Session session )
	{
		Session = session;
		MinimumWidth = 256;

		_tracks = new SynchronizedSet<TrackView, TimelineTrack>(
			AddTrack, RemoveTrack, UpdateTrack );

		_backgroundItem = new BackgroundItem( Session );
		Add( _backgroundItem );

		_gridItem = new GridItem( Session );
		Add( _gridItem );

		_currentPointerItem = new CurrentPointerItem( Theme.Yellow );
		Add( _currentPointerItem );

		_previewPointerItem = new CurrentPointerItem( Theme.Blue );
		Add( _previewPointerItem );

		ScrubBarTop = new ScrubberItem( Session.Editor, true ) { Size = new Vector2( Width, 24f ) };
		Add( ScrubBarTop );
		ScrubBarBottom = new ScrubberItem( Session.Editor, false ) { Size = new Vector2( Width, 24f ) };
		Add( ScrubBarBottom );

		Session.PlayheadChanged += UpdateCurrentPosition;
		Session.PreviewChanged += UpdatePreviewPosition;
		Session.ViewChanged += UpdateView;

		FocusMode = FocusMode.TabOrClickOrWheel;

		AcceptDrops = true;

		var bg = new Pixmap( 8 );
		bg.Clear( Colors.Background );

		SetBackgroundImage( bg );

		Antialiasing = true;

		ToolTip =
			"""
			<h3>Timeline</h3>
			<p><b>Scroll</b> to scroll vertically through track list.</p>
			<p><b>Shift+Scroll</b> or <b>Middle-Click+Drag</b> to pan horizontally.</p>
			<p><b>Ctrl+Scroll</b> to zoom in / out.</p>
			<p><b>Alt+Scroll</b> to scrub forwards / backwards by a frame.</p>
			<p><b>Hold Shift</b> to smoothly preview the time under the mouse cursor.</p>
			""";
	}

	public override void OnDestroyed()
	{
		DeleteAllItems();

		Session.PlayheadChanged -= UpdateCurrentPosition;
		Session.PreviewChanged -= UpdatePreviewPosition;
		Session.ViewChanged -= UpdateView;
	}

	private int _lastState;
	private int _lastVisibleRectHash;

	[EditorEvent.Frame]
	public void Frame()
	{
		UpdateScrubBars();
		UpdateTracksIfNeeded();

		var visibleRectHash = VisibleRect.GetHashCode();

		if ( visibleRectHash != _lastVisibleRectHash )
		{
			Session.DispatchViewChanged();

			if ( (Application.KeyboardModifiers & KeyboardModifiers.Shift) != 0 )
			{
				UpdatePreviewTime( _lastMouseLocalPos );
			}
		}

		_lastVisibleRectHash = visibleRectHash;

		if ( Session.PreviewTime is not null
			&& (Application.KeyboardModifiers & KeyboardModifiers.Shift) == 0
			&& (Application.MouseButtons & MouseButtons.Left) == 0 )
		{
			Session.PreviewTime = null;
		}
	}

	private void UpdateTracksIfNeeded()
	{
		var state = HashCode.Combine( Session.PixelsPerSecond, Session.TimeOffset, Session.FrameRate, Session.TrackList.StateHash );

		if ( state == _lastState ) return;

		_lastState = state;

		UpdateTracks();
		Update();
	}

	private void UpdateView()
	{
		UpdateSceneFrame();
		UpdateScrubBars();

		UpdateCurrentPosition( Session.PlayheadTime );
		UpdatePreviewPosition( Session.PreviewTime );

		UpdateTracksIfNeeded();
	}

	private void UpdateScrubBars()
	{
		_backgroundItem.Update();

		ScrubBarTop.PrepareGeometryChange();
		ScrubBarBottom.PrepareGeometryChange();

		var visibleRect = VisibleRect;

		ScrubBarTop.Position = visibleRect.TopLeft;
		ScrubBarBottom.Position = visibleRect.BottomLeft - new Vector2( 0f, ScrubBarBottom.Height );

		ScrubBarTop.Width = Width;
		ScrubBarBottom.Width = Width;

		ScrubBarTop.UpdateCursor();
		ScrubBarBottom.UpdateCursor();
	}

	protected override void OnResize()
	{
		base.OnResize();

		UpdateScrubBars();
		UpdateTracks();
	}

	private void UpdateCurrentPosition( MovieTime time )
	{
		_currentPointerItem.PrepareGeometryChange();

		_currentPointerItem.Position = new Vector2( Session.TimeToPixels( time ), VisibleRect.Top + 12f );
		_currentPointerItem.Size = new Vector2( 1, VisibleRect.Height - 24f );
	}

	private void UpdatePreviewPosition( MovieTime? time )
	{
		_previewPointerItem.PrepareGeometryChange();

		if ( time is not null )
		{
			_previewPointerItem.Position = new Vector2( Session.TimeToPixels( time.Value ), VisibleRect.Top + 12f );
			_previewPointerItem.Size = new Vector2( 1, VisibleRect.Height - 24f );
		}
		else
		{
			_previewPointerItem.Position = new Vector2( -50000f, 0f );
		}
	}

	void UpdateSceneFrame()
	{
		Session.TrackListViewHeight = Height - 64f;

		var x = Session.TimeToPixels( Session.TimeOffset );
		SceneRect = new Rect( x - 8, Session.TrackListScrollPosition - Session.TrackListScrollOffset, Width - 4, Height - 4 ); // I don't know where the fuck this 4 comes from, but it stops it having some scroll

		_backgroundItem.PrepareGeometryChange();
		_backgroundItem.SceneRect = SceneRect;
		_backgroundItem.Update();

		_gridItem.PrepareGeometryChange();
		_gridItem.SceneRect = SceneRect;
		_gridItem.Update();

		UpdateCurrentPosition( Session.PlayheadTime );
		UpdatePreviewPosition( Session.PreviewTime );
	}

	public void UpdateTracks()
	{
		UpdateSceneFrame();

		_tracks.Update( Session.TrackList.VisibleTracks );

		Update();
	}

	private TimelineTrack AddTrack( TrackView source )
	{
		var item = new TimelineTrack( this, source );

		Add( item );

		return item;
	}

	private void RemoveTrack( TimelineTrack item ) => item.Destroy();
	private bool UpdateTrack( TrackView source, TimelineTrack item )
	{
		item.UpdateLayout();

		return true;
	}

	protected override void OnWheel( WheelEvent e )
	{
		base.OnWheel( e );

		Session.EditMode?.MouseWheel( e );

		if ( e.Accepted ) return;

		// scoll
		if ( e.HasShift )
		{
			Session.ScrollBy( -e.Delta / 10.0f * (Session.PixelsPerSecond / 10.0f), true );
			e.Accept();
			return;
		}

		// zoom
		if ( e.HasCtrl )
		{
			Session.Zoom( e.Delta / 10.0f, _lastMouseTime );
			e.Accept();
			return;
		}

		// scrub
		if ( e.HasAlt )
		{
			var dt = MovieTime.FromFrames( 1, Session.FrameRate );
			var nextTime = Session.PlayheadTime.Round( dt ) + Math.Sign( e.Delta ) * dt;

			Session.PlayheadTime = nextTime;
			e.Accept();
			return;
		}

		Session.TrackListScrollPosition -= e.Delta / 5f;
		e.Accept();
	}

	private Vector2 _lastMouseLocalPos;
	private MovieTime _lastMouseTime;

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		var delta = e.LocalPosition - _lastMouseLocalPos;

		if ( e.ButtonState == MouseButtons.Left && IsDragging )
		{
			Drag( ToScene( e.LocalPosition ) );
			e.Accepted = true;
			return;
		}

		if ( e.ButtonState == MouseButtons.Middle )
		{
			Session.ScrollBy( delta.x, false );
		}

		if ( e.ButtonState == MouseButtons.Right )
		{
			Session.PlayheadTime = Session.ScenePositionToTime( ToScene( e.LocalPosition ), SnapFlag.Playhead );
		}

		if ( e.HasShift )
		{
			UpdatePreviewTime( e.LocalPosition );
		}

		_lastMouseLocalPos = e.LocalPosition;
		_lastMouseTime = Session.PixelsToTime( ToScene( e.LocalPosition ).x );

		Session.EditMode?.MouseMove( e );
	}

	private void UpdatePreviewTime( Vector2 localPos )
	{
		Session.PreviewTime = Application.MouseButtons != 0
			? Session.ScenePositionToTime( ToScene( localPos ) )
			: Session.PixelsToTime( ToScene( localPos ).x );
	}

	public new GraphicsItem? GetItemAt( Vector2 scenePosition )
	{
		// TODO: Is there a nicer way?

		var oldGridZIndex = _gridItem.ZIndex;

		_gridItem.ZIndex = -1000;

		var item = base.GetItemAt( scenePosition );

		_gridItem.ZIndex = oldGridZIndex;

		return item;
	}

	private readonly List<IMovieDraggable> _draggedItems = new();
	private IMovieDraggable? _primaryDraggedItem;
	private MovieTime _lastDragTime;
	private MovieTime _minDragTime;
	private SnapOptions _dragSnapOptions;

	public bool IsDragging => _primaryDraggedItem is not null;

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		DragType = DragTypes.None;

		if ( e.ButtonState == MouseButtons.Middle )
		{
			e.Accepted = true;
			return;
		}

		var scenePos = ToScene( e.LocalPosition );

		if ( GetItemAt( scenePos ) is { Selectable: true } item )
		{
			if ( e.LeftMouseButton && !e.HasCtrl && item is IMovieDraggable draggable )
			{
				e.Accepted = StartDragging( scenePos, draggable );
			}

			return;
		}

		Session.EditMode?.MousePress( e );

		if ( e.Accepted ) return;

		if ( e.LeftMouseButton )
		{
			DragType = DragTypes.SelectionRect;
			return;
		}

		if ( e.RightMouseButton )
		{
			e.Accepted = true;
			Session.PlayheadTime = Session.ScenePositionToTime( ToScene( e.LocalPosition ), SnapFlag.Playhead );
			return;
		}
	}

	private bool StartDragging( Vector2 scenePos, IMovieDraggable draggable )
	{
		if ( draggable is not GraphicsItem item ) return false;

		if ( !item.Selected )
		{
			DeselectAll();
			item.Selected = true;
		}

		_draggedItems.Clear();
		_draggedItems.AddRange( SelectedItems.OfType<IMovieDraggable>() );

		var ignoreBlocks = _draggedItems
			.Select( x => x.Block )
			.OfType<ITrackBlock>()
			.Distinct()
			.ToImmutableHashSet();

		_primaryDraggedItem = draggable;
		_lastDragTime = Session.ScenePositionToTime( scenePos, new SnapOptions( SnapFlag.TrackBlock ) );

		var snapOffsets = _draggedItems
			.SelectMany( x => new [] { x.TimeRange.Start - _lastDragTime, x.TimeRange.End - _lastDragTime } )
			.Order()
			.Distinct()
			.ToArray();

		_dragSnapOptions = new SnapOptions( IgnoreBlocks: ignoreBlocks, SnapOffsets: snapOffsets );
		_minDragTime = -snapOffsets[0];

		return true;
	}

	private void Drag( Vector2 scenePos )
	{
		var time = MovieTime.Max( _minDragTime, Session.ScenePositionToTime( scenePos, _dragSnapOptions ) );
		var delta = time - _lastDragTime;

		if ( delta.IsZero ) return;

		_lastDragTime = time;

		foreach ( var item in _draggedItems )
		{
			item.Drag( delta );
		}

		Session.EditMode?.DragItems( _draggedItems, delta );
	}

	private void StopDragging()
	{
		_draggedItems.Clear();
		_primaryDraggedItem = null;
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );

		if ( _primaryDraggedItem is not null )
		{
			StopDragging();
			e.Accepted = true;
			return;
		}

		Session.EditMode?.MouseRelease( e );
	}

	public void DeselectAll()
	{
		Session.TrackList.DeselectAll();

		foreach ( var item in SelectedItems.ToArray() )
		{
			item.Selected = false;
		}
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		Session.EditMode?.KeyPress( e );

		if ( e.Accepted ) return;

		if ( e.Key == KeyCode.Shift )
		{
			e.Accepted = true;
			Session.PreviewTime = Session.ScenePositionToTime( ToScene( _lastMouseLocalPos ) );
		}
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		base.OnKeyRelease( e );

		Session.EditMode?.KeyRelease( e );
	}

	private MovieResource? GetDraggedClip( DragData data )
	{
		if ( data.Assets.FirstOrDefault( x => x.AssetPath?.EndsWith( ".movie" ) ?? false ) is not { } assetData )
		{
			return null;
		}

		var assetTask = assetData.GetAssetAsync();

		if ( !assetTask.IsCompleted ) return null;
		if ( assetTask.Result?.LoadResource<MovieResource>() is not { } resource ) return null;

		if ( !Session.CanReferenceMovie( resource ) ) return null;

		return resource;
	}

	private ProjectSequenceTrack? _draggedTrack;
	private ProjectSequenceBlock? _draggedBlock;
	private readonly HashSet<ITrackBlock> _draggedBlocks = new();

	public override void OnDragHover( DragEvent ev )
	{
		if ( _draggedBlock is null || _draggedTrack is null )
		{
			if ( GetDraggedClip( ev.Data ) is not { } resource )
			{
				ev.Action = DropAction.Ignore;
				return;
			}

			var clip = resource.GetCompiled();

			_draggedTrack = Session.GetOrCreateTrack( resource );
			_draggedBlock = _draggedTrack.AddBlock( (0d, clip.Duration), default, resource );

			Session.TrackList.Update();
			UpdateTracksIfNeeded();
		}

		_draggedBlocks.Clear();
		_draggedBlocks.Add( _draggedBlock );

		var time = Session.ScenePositionToTime( ToScene( ev.LocalPosition ),
			new SnapOptions( IgnoreBlocks: _draggedBlocks ) );

		_draggedBlock.TimeRange = (time, time + _draggedBlock.TimeRange.Duration);
		_draggedBlock.Transform = new MovieTransform( -time );

		Session.TrackList.Find( _draggedTrack )?.MarkValueChanged();

		Log.Info( time );

		ev.Action = DropAction.Link;
	}

	public override void OnDragLeave()
	{
		base.OnDragLeave();

		if ( _draggedBlock is { } block && _draggedTrack is { } track )
		{
			track.RemoveBlock( block );

			if ( track.IsEmpty )
			{
				track.Remove();
			}

			_draggedTrack = null;
			_draggedBlock = null;
		}
	}

	public override void OnDragDrop( DragEvent ev )
	{
		if ( GetDraggedClip( ev.Data ) is not { } movie )
		{
			return;
		}

		_draggedTrack = null;
		_draggedBlock = null;
	}

	public void GetSnapTimes( ref TimeSnapHelper snap )
	{
		var mouseScenePos = ToScene( _lastMouseLocalPos );

		if ( mouseScenePos.y <= ScrubBarTop.SceneRect.Bottom || mouseScenePos.y >= ScrubBarBottom.SceneRect.Top )
		{
			snap.Add( SnapFlag.MinorTick, snap.Time.Round( Session.MinorTick.Interval ), -2, force: true );
			snap.Add( SnapFlag.MajorTick, snap.Time.Round( Session.MajorTick.Interval ), -1 );
		}

		if ( Session.EditMode?.SourceTimeRange is { } pasteRange )
		{
			snap.Add( SnapFlag.PasteBlock, pasteRange.Start );
			snap.Add( SnapFlag.PasteBlock, pasteRange.End );
		}

		foreach ( var dopeTrack in _tracks )
		{
			if ( dopeTrack.View == snap.Options.IgnoreTrack ) continue;
			if ( dopeTrack.View.IsLocked ) continue;
			if ( mouseScenePos.y < dopeTrack.SceneRect.Top ) continue;
			if ( mouseScenePos.y > dopeTrack.SceneRect.Bottom ) continue;

			foreach ( var block in dopeTrack.View.Blocks )
			{
				if ( snap.Options.IgnoreBlocks?.Contains( block ) is true ) continue;

				snap.Add( SnapFlag.TrackBlock, block.TimeRange.Start );
				snap.Add( SnapFlag.TrackBlock, block.TimeRange.End );
			}
		}
	}
}
