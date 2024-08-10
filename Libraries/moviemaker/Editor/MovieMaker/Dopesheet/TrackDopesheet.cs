using System.Linq;

namespace Editor.MovieMaker;

public class TrackDopesheet : GraphicsView
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
	CurrentPointerItem currentPointerItem;

	public TrackDopesheet( TrackListWidget timelineTracklist )
	{
		this.tracklist = timelineTracklist;
		MinimumWidth = 256;

		gridItem = new GridItem();
		Add( gridItem );

		currentPointerItem = new CurrentPointerItem();
		Add( currentPointerItem );

		Session.OnPointerChanged += UpdatePointerPosition;

		FocusMode = FocusMode.TabOrClickOrWheel;

		var bg = new Pixmap( 8 );
		bg.Clear( TrackDopesheet.Colors.Background );

		SetBackgroundImage( bg );
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();

		Session.OnPointerChanged -= UpdatePointerPosition;
	}

	int lastState;

	[EditorEvent.Frame]
	public void Frame()
	{
		var state = HashCode.Combine( Session.TimeVisible, Session.TimeOffset );

		if ( state != lastState )
		{
			lastState = state;
			UpdateTracks();
			Update();
		}
	}

	protected override void OnResize()
	{
		base.OnResize();

		UpdateTracks();
	}

	void UpdatePointerPosition( float time )
	{
		currentPointerItem.Position = new Vector2( Session.TimeToPixels( time ), 0 );
		currentPointerItem.Size = new Vector2( 1, Height );
	}

	void UpdateSceneFrame()
	{
		var x = Session.TimeToPixels( Session.TimeOffset );
		SceneRect = new Rect( x - 8, 0, Width - 4, Height - 4 ); // I don't know where the fuck this 4 comes from, but it stops it having some scroll
		gridItem.SceneRect = SceneRect;
		gridItem.Update();

		UpdatePointerPosition( Session.CurrentPointer );
	}

	public void UpdateTracks()
	{
		var maxWidth = Session.TimeToPixels( tracklist.MaxTimeVisible );

		UpdateSceneFrame();

		foreach ( var track in tracklist.Tracks )
		{
			if ( track.Channel is null )
			{
				track.Channel = new DopesheetTrack( track );
				track.Channel.Read();
				Add( track.Channel );
			}

			track.Channel.PositionHandles();
		}

		Update();
	}

	protected override void OnWheel( WheelEvent e )
	{
		base.OnWheel( e );

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

		if ( e.ButtonState == MouseButtons.Right )
		{
			Session.SetCurrentPointer( Session.PixelsToTime( ToScene( e.LocalPosition ).x ) );
		}

		lastpos = e.LocalPosition;
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		DragType = DragTypes.None;

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

	}

	protected override void OnKeyPress( KeyEvent e )
	{
		if ( e.Key == KeyCode.Left )
		{
			foreach ( var h in SelectedItems.OfType<DopeHandle>() )
			{
				h.Nudge( e.HasShift ? -1.0f : -0.1f );
			}

			e.Accepted = true;
			tracklist.WriteTracks();
			return;
		}

		if ( e.Key == KeyCode.Right )
		{
			foreach ( var h in SelectedItems.OfType<DopeHandle>() )
			{
				h.Nudge( e.HasShift ? 1.0f : 0.1f );
			}

			e.Accepted = true;
			tracklist.WriteTracks();
			return;
		}

		base.OnKeyPress( e );
	}

	record struct CopiedHandle( Guid track, float time, object value );
	static List<CopiedHandle> copied;

	public void OnCopy()
	{
		copied = new();

		foreach ( var handle in SelectedItems.OfType<DopeHandle>() )
		{
			copied.Add( new CopiedHandle( handle.Track.Track.Source.Id, handle.Time, handle.Value ) );
		}
	}

	public void OnPaste()
	{
		if ( copied is null || copied.Count == 0 )
			return;

		var pastePointer = Session.CurrentPointer;
		pastePointer -= copied.Min( x => x.time );

		foreach ( var entry in copied )
		{
			Log.Info( $"{entry.value}" );

			var track = tracklist.Tracks.FirstOrDefault( x => x.Source.Id == entry.track );
			if ( track is null ) continue;

			track.AddKey( entry.time + pastePointer, entry.value );
		}

		tracklist.WriteTracks();
	}


	public void OnDelete()
	{
		foreach ( var h in SelectedItems.OfType<DopeHandle>() )
		{
			h.Destroy();
		}

		tracklist.WriteTracks();
	}


}
