using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public class DopeSheet : GraphicsView
{
	public static class Colors
	{
		public static Color Background => Theme.ControlBackground.Lighten( 0.1f );
		public static Color ChannelBackground => Theme.ControlBackground.Darken( 0.1f );
		public static Color HandleSelected => Theme.White;
	}

	private TrackListWidget TrackList { get; }
	private Session Session => TrackList.Session;

	private readonly GridItem _gridItem;

	private readonly CurrentPointerItem _currentPointerItem;
	private readonly CurrentPointerItem _previewPointerItem;

	public ScrubberItem ScrubBarTop { get; }
	public ScrubberItem ScrubBarBottom { get; }

	public Rect VisibleRect
	{
		get
		{
			var screenRect = TrackList.ScreenRect;

			screenRect.Left = TrackList.RightWidget.ScreenRect.Left;

			var topLeft = FromScreen( screenRect.TopLeft );
			var bottomRight = FromScreen( screenRect.BottomRight );

			return ToScene( new Rect( topLeft, bottomRight - topLeft ) );
		}
	}

	public DopeSheet( TrackListWidget trackList )
	{
		TrackList = trackList;
		MinimumWidth = 256;

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

		Session.PointerChanged += UpdateCurrentPosition;
		Session.PreviewChanged += UpdatePreviewPosition;
		Session.ViewChanged += UpdateView;

		FocusMode = FocusMode.TabOrClickOrWheel;

		var bg = new Pixmap( 8 );
		bg.Clear( Colors.Background );

		SetBackgroundImage( bg );

		Antialiasing = true;
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();

		Session.PointerChanged -= UpdateCurrentPosition;
		Session.PreviewChanged -= UpdatePreviewPosition;
		Session.ViewChanged -= UpdateView;
	}

	private int _lastState;
	private int _lastVisibleRectHash;

	[EditorEvent.Frame]
	public void Frame()
	{
		UpdateScrubBars();

		var state = HashCode.Combine( Session.PixelsPerSecond, Session.TimeOffset, Session.FrameRate );

		if ( state != _lastState )
		{
			UpdateTracks();
			Update();
		}

		var visibleRectHash = VisibleRect.GetHashCode();

		if ( visibleRectHash != _lastVisibleRectHash || state != _lastState )
		{
			Session.DispatchViewChanged();
		}

		_lastState = state;
		_lastVisibleRectHash = visibleRectHash;

		if ( (Application.KeyboardModifiers & KeyboardModifiers.Shift) == 0 && Session.PreviewPointer is not null )
		{
			Session.ClearPreviewPointer();
		}
	}

	private void UpdateView()
	{
		UpdateScrubBars();

		UpdateCurrentPosition( Session.CurrentPointer );
		UpdatePreviewPosition( Session.PreviewPointer );
	}

	private void UpdateScrubBars()
	{
		ScrubBarTop.PrepareGeometryChange();
		ScrubBarBottom.PrepareGeometryChange();

		var visibleRect = VisibleRect;

		ScrubBarTop.Position = visibleRect.TopLeft;
		ScrubBarBottom.Position = visibleRect.BottomLeft - new Vector2( 0f, ScrubBarBottom.Height );

		ScrubBarTop.Width = Width;
		ScrubBarBottom.Width = Width;
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
		var x = Session.TimeToPixels( Session.TimeOffset );
		SceneRect = new Rect( x - 8, 0, Width - 4, Height - 4 ); // I don't know where the fuck this 4 comes from, but it stops it having some scroll
		_gridItem.SceneRect = SceneRect;
		_gridItem.Update();

		UpdateCurrentPosition( Session.CurrentPointer );
		UpdatePreviewPosition( Session.PreviewPointer );
	}

	public void UpdateTracks()
	{
		UpdateSceneFrame();

		foreach ( var widget in TrackList.Tracks )
		{
			if ( widget.Target is not ITrackProperty { CanWrite: true } ) continue;
			if ( widget.ProjectTrack is not IProjectPropertyTrack propertyTrack ) continue;

			if ( widget.DopeSheetTrack is null )
			{
				widget.DopeSheetTrack = new DopeSheetTrack( widget, propertyTrack );
				Add( widget.DopeSheetTrack );

				Session.EditMode?.TrackAdded( widget.DopeSheetTrack );
			}
		}

		Update();

		foreach ( var track in TrackList.Tracks )
		{
			track.UpdateChannelPosition();
		}
	}

	protected override void OnWheel( WheelEvent e )
	{
		base.OnWheel( e );

		Session.EditMode?.MouseWheel( e );

		if ( e.Accepted ) return;

		if ( GetAncestor<TrackListWidget>().OnCanvasWheel( e ) )
		{
			e.Accept();
		}
	}

	private Vector2 _lastMouseLocalPos;

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		var delta = e.LocalPosition - _lastMouseLocalPos;

		if ( e.ButtonState == MouseButtons.Middle )
		{
			//var delta = Application.CursorDelta.x;
			TrackList.ScrollBy( delta.x );
		}

		if ( e.ButtonState == MouseButtons.Right )
		{
			Session.SetCurrentPointer( Session.ScenePositionToTime( ToScene( e.LocalPosition ), ignore: SnapFlag.PlayHead ) );
		}

		if ( e.HasShift )
		{
			Session.SetPreviewPointer( e.ButtonState != 0
				? Session.ScenePositionToTime( ToScene( e.LocalPosition ) )
				: Session.PixelsToTime( ToScene( e.LocalPosition ).x ) );
		}

		_lastMouseLocalPos = e.LocalPosition;

		Session.EditMode?.MouseMove( e );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		DragType = DragTypes.None;

		Session.EditMode?.MousePress( e );

		if ( e.Accepted ) return;

		if ( e.ButtonState == MouseButtons.Left )
		{
			DragType = DragTypes.SelectionRect;
			return;
		}

		if ( e.ButtonState == MouseButtons.Right )
		{
			Session.SetCurrentPointer( Session.ScenePositionToTime( ToScene( e.LocalPosition ), ignore: SnapFlag.PlayHead ) );
			return;
		}
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );

		Session.EditMode?.MouseRelease( e );
	}

	protected override void OnKeyPress( KeyEvent e )
	{
		base.OnKeyPress( e );

		Session.EditMode?.KeyPress( e );

		if ( e.Accepted ) return;

		if ( e.Key == KeyCode.Shift )
		{
			e.Accepted = true;
			Session.SetPreviewPointer( Session.ScenePositionToTime( ToScene( _lastMouseLocalPos ) ) );
		}
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		base.OnKeyRelease( e );

		Session.EditMode?.KeyRelease( e );
	}

	public void GetSnapTimes( ref TimeSnapHelper snap )
	{
		var mouseScenePos = ToScene( _lastMouseLocalPos );

		if ( mouseScenePos.y <= ScrubBarTop.SceneRect.Bottom || mouseScenePos.y >= ScrubBarBottom.SceneRect.Top )
		{
			snap.Add( SnapFlag.MinorTick, snap.Time.Round( Session.MinorTick.Interval ), -2, force: true );
			snap.Add( SnapFlag.MajorTick, snap.Time.Round( Session.MajorTick.Interval ), -1 );
		}

		if ( Session.EditMode?.PasteTimeRange is { } pasteRange )
		{
			snap.Add( SnapFlag.PasteBlock, pasteRange.Start );
			snap.Add( SnapFlag.PasteBlock, pasteRange.End );
		}

		foreach ( var trackWidget in TrackList.Tracks )
		{
			if ( trackWidget.ProjectTrack is not IProjectPropertyTrack propertyTrack ) continue;
			if ( trackWidget.DopeSheetTrack is not { Visible: true } dopeTrack ) continue;
			if ( trackWidget.IsLocked ) continue;
			if ( mouseScenePos.y < dopeTrack.SceneRect.Top ) continue;
			if ( mouseScenePos.y > dopeTrack.SceneRect.Bottom ) continue;

			foreach ( var block in propertyTrack.Blocks )
			{
				snap.Add( SnapFlag.TrackBlock, block.TimeRange.Start );
				snap.Add( SnapFlag.TrackBlock, block.TimeRange.End );
			}
		}
	}
}
