using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

public class DopeSheet : GraphicsView
{
	public static class Colors
	{
		public static Color Background => Theme.ControlBackground.Lighten( 0.1f );
		public static Color ChannelBackground => Theme.ControlBackground.Darken( 0.1f );
		public static Color HandleSelected => Theme.White;
	}

	private TrackListWidget tracklist;
	private Session Session => tracklist.Session;

	GridItem gridItem;

	private CurrentPointerItem _currentPointerItem;
	private CurrentPointerItem _previewPointerItem;

	public DopeSheet( TrackListWidget timelineTracklist )
	{
		this.tracklist = timelineTracklist;
		MinimumWidth = 256;

		gridItem = new GridItem();
		Add( gridItem );

		_currentPointerItem = new CurrentPointerItem( Theme.Yellow );
		Add( _currentPointerItem );

		_previewPointerItem = new CurrentPointerItem( Theme.Blue );
		Add( _previewPointerItem );

		Session.PointerChanged += UpdateCurrentPosition;
		Session.PreviewChanged += UpdatePreviewPosition;

		FocusMode = FocusMode.TabOrClickOrWheel;

		var bg = new Pixmap( 8 );
		bg.Clear( DopeSheet.Colors.Background );

		SetBackgroundImage( bg );

		Antialiasing = true;
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();

		Session.PointerChanged -= UpdateCurrentPosition;
		Session.PreviewChanged -= UpdatePreviewPosition;
	}

	int lastState;

	[EditorEvent.Frame]
	public void Frame()
	{
		var state = HashCode.Combine( Session.PixelsPerSecond, Session.TimeOffset );

		if ( state != lastState )
		{
			lastState = state;
			UpdateTracks();
			Update();
		}

		if ( (Application.KeyboardModifiers & KeyboardModifiers.Shift) == 0 && Session.PreviewPointer is not null )
		{
			Session.ClearPreviewPointer();
		}
	}

	protected override void OnResize()
	{
		base.OnResize();

		UpdateTracks();
	}

	private void UpdateCurrentPosition( float time )
	{
		_currentPointerItem.Position = new Vector2( Session.TimeToPixels( time ), 0 );
		_currentPointerItem.Size = new Vector2( 1, Height );
	}

	private void UpdatePreviewPosition( float? time )
	{
		if ( time is not null )
		{
			_previewPointerItem.Position = new Vector2( Session.TimeToPixels( time.Value ), 0 );
			_previewPointerItem.Size = new Vector2( 1, Height );
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
		gridItem.SceneRect = SceneRect;
		gridItem.Update();

		UpdateCurrentPosition( Session.CurrentPointer );
		UpdatePreviewPosition( Session.PreviewPointer );
	}

	public void UpdateTracks()
	{
		var maxWidth = Session.TimeToPixels( tracklist.MaxTimeVisible );

		UpdateSceneFrame();

		foreach ( var track in tracklist.Tracks )
		{
			if ( track.Property is ISceneReferenceMovieProperty ) continue;

			if ( track.DopeSheetTrack is null )
			{
				track.DopeSheetTrack = new DopeSheetTrack( track );
				Add( track.DopeSheetTrack );

				Session.EditMode?.TrackAdded( track.DopeSheetTrack );
			}
		}

		Update();

		foreach ( var track in tracklist.Tracks )
		{
			track.UpdateChannelPosition();
		}

		// Scrub bar position depends on width of track headers

		tracklist.Editor.UpdateScrubBars();
	}

	protected override void OnWheel( WheelEvent e )
	{
		base.OnWheel( e );

		Session.EditMode?.MouseWheel( e );

		// TODO: Can't check if event was accepted?

		if ( e.HasShift )
		{
			return;
		}

		if ( GetAncestor<TrackListWidget>().OnCanvasWheel( e ) )
		{
			e.Accept();
		}

		return;
	}

	Vector2 lastpos;

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		var delta = e.LocalPosition - lastpos;

		if ( e.ButtonState == MouseButtons.Middle )
		{
			//var delta = Application.CursorDelta.x;
			tracklist.ScrollBy( delta.x );
		}

		var time = Session.PixelsToTime( ToScene( e.LocalPosition ).x );

		if ( e.ButtonState == MouseButtons.Right )
		{
			Session.SetCurrentPointer( time );
		}

		if ( e.HasShift )
		{
			Session.SetPreviewPointer( time );
		}

		lastpos = e.LocalPosition;

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
			Session.SetCurrentPointer( Session.PixelsToTime( ToScene( e.LocalPosition ).x ) );
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
			Session.SetPreviewPointer( Session.PixelsToTime( ToScene( lastpos ).x ) );
		}
	}

	protected override void OnKeyRelease( KeyEvent e )
	{
		base.OnKeyRelease( e );

		Session.EditMode?.KeyRelease( e );
	}

	public void OnCopy()
	{
		Session.EditMode?.Copy();
	}

	public void OnPaste()
	{
		Session.EditMode?.Paste();
	}

	public void OnDelete()
	{
		Session.EditMode?.Delete();
	}
}
