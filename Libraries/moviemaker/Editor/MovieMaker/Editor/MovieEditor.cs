using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;


public class MovieEditor : Widget
{
	public ScrubberWidget ScrubBar { get; private set; }
	public TrackListWidget TrackList { get; private set; }
	public ToolbarWidget Toolbar { get; private set; }


	public Session Session { get; private set; }

	public MovieEditor( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		FocusMode = FocusMode.TabOrClickOrWheel;

		UpdateEditorContext();

		if ( Session is null )
		{
			CreateStartupHelper();
		}
	}

	public void Initialize( MovieClipPlayer player )
	{
		Layout.Clear( true );

		player.clip ??= new MovieClip();

		Session = new Session();
		Session.SetClip( player.clip );
		Session.Current = Session;

		Layout?.Clear( true );
		Toolbar = Layout.Add( new ToolbarWidget( this ) );
		ScrubBar = Layout.Add( new ScrubberWidget( this ) );
		TrackList = Layout.Add( new TrackListWidget( this ) );
	}

	void CloseSession()
	{
		Layout.Clear( true );
		Session = null;
		ScrubBar = default;
		TrackList = default;
		Toolbar = default;

		CreateStartupHelper();
	}

	void CreateStartupHelper()
	{
		var row = Layout.AddRow();

		row.AddStretchCell();

		var col = row.AddColumn();
		col.AddStretchCell();

		col.Add( new Label( "Create a Timeline Player component to get started. The\nTimeline Player is responsible for playing the clip in game." ) );
		col.AddSpacingCell( 32 );
		col.Add( new Label( "The Timeline Clip is stored in the player component right \nnow. Eventually we should let you save the clip\ndata in an asset file." ) );
		col.AddSpacingCell( 32 );

		var button = col.Add( new Button.Primary( "Create Player Component", "add_circle" ) );
		button.Clicked = CreateNew;


		col.AddStretchCell();

		row.AddStretchCell();
	}

	[EditorEvent.Frame]
	public void Frame()
	{
		UpdateEditorContext();

		Session?.Frame();
	}

	[Shortcut( "timeline.playtoggle", "Space", ShortcutType.Window )]
	public void PlayToggle()
	{
		if ( Session is null )
			return;

		if ( !Session.Playing )
		{
			Session.Play();
		}
		else
		{
			Session.Stop();
		}
	}

	[Shortcut( "timeline.copy", "CTRL+C", ShortcutType.Widget )]
	public void OnCopy()
	{
		TrackList?.OnCopy();
	}

	[Shortcut( "timeline.paste", "CTRL+V", ShortcutType.Widget )]
	public void OnPaste()
	{
		TrackList?.OnPaste();
	}

	[Shortcut( "timeline.delete", "DEL", ShortcutType.Widget )]
	public void OnDelete()
	{
		TrackList?.OnDelete();
	}

	int contextHash;

	List<MovieClipPlayer> playersAvailable = new();

	/// <summary>
	/// Look for any clips we can edit. If the clip we're editing has gone - stop editing it.
	/// </summary>
	void UpdateEditorContext()
	{
		HashCode hash = new HashCode();
		var allplayers = SceneEditorSession.Active.Scene.GetAllComponents<MovieClipPlayer>();
		foreach ( var player in allplayers )
		{
			hash.Add( player );
		}

		var hc = hash.ToHashCode();

		if ( contextHash == hc ) return;
		contextHash = hc;

		playersAvailable.Clear();
		playersAvailable.AddRange( allplayers );

		// The current session exists
		if ( Session is not null )
		{
			// Whatever we were editing doesn't exist anymore!
			if ( !playersAvailable.Any( x => x.clip == Session.Clip ) )
			{
				CloseSession();
			}
		}

		// session is null, lets load the first player
		if ( Session is null )
		{
			if ( playersAvailable.Count == 0 ) return;
			Initialize( playersAvailable.First() );
		}

		Toolbar.UpdatePlayers( playersAvailable );
	}

	public void Switch( MovieClipPlayer player )
	{
		Initialize( player );
		contextHash = default;
	}

	public void CreateNew()
	{
		using ( SceneEditorSession.Active.Scene.Push() )
		{
			var go = new GameObject( true, "New Timeline Player" );
			go.Components.Create<MovieClipPlayer>();

			SceneEditorSession.Active.Selection.Set( go );
		}

		contextHash = default;
	}
}

