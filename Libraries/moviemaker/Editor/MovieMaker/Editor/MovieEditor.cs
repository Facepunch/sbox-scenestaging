using Sandbox;
using Sandbox.MovieMaker;
using System.Linq;
using System.Reflection;

namespace Editor.MovieMaker;


public class MovieEditor : Widget
{
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

	public void Initialize( MoviePlayer player )
	{
		Log.Info( $"Initialize: {player.GameObject.Name}" );

		Layout.Clear( true );

		if ( player.MovieClip is null )
		{
			// Default to an embedded clip, rather than a resource file

			player.EmbeddedClip ??= new MovieClip();
		}

		Session = new Session { Editor = this };
		Session.SetPlayer( player );
		Session.Current = Session;

		Layout?.Clear( true );
		Toolbar = Layout.Add( new ToolbarWidget( this ) );
		TrackList = Layout.Add( new TrackListWidget( this ) );

		Session.RestoreFromCookies();
	}

	void CloseSession()
	{
		Log.Info( "Close session" );

		Layout.Clear( true );
		Session = null;
		TrackList = null;
		Toolbar = null;

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

	[Shortcut( "editor.save", "CTRL+S" )]
	public void OnSave()
	{
		Session?.Save();
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

	List<MoviePlayer> playersAvailable = new();

	/// <summary>
	/// Look for any clips we can edit. If the clip we're editing has gone - stop editing it.
	/// </summary>
	void UpdateEditorContext()
	{
		HashCode hash = new HashCode();

		if ( SceneEditorSession.Active?.Scene is not { } scene ) return;

		var allplayers = scene.GetAllComponents<MoviePlayer>();

		foreach ( var player in allplayers )
		{
			hash.Add( player );
			hash.Add( player.MovieClip );
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
			if ( playersAvailable.All( x => x.MovieClip != Session.Clip || x != Session.Player ) )
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
		Toolbar.UpdateClips();
	}

	public void Switch( MoviePlayer player )
	{
		Initialize( player );
		contextHash = default;
	}

	public void CreateNew()
	{
		using ( SceneEditorSession.Active.Scene.Push() )
		{
			var go = new GameObject( true, "New Timeline Player" );
			go.Components.Create<MoviePlayer>();

			SceneEditorSession.Active.Selection.Set( go );
		}

		contextHash = default;
	}

	public void SwitchToEmbedded()
	{
		if ( Session.Clip == Session.Player.EmbeddedClip ) return;

		Session.Player.EmbeddedClip = Session.Clip?.Clone();

		Switch( Session.Player );
	}

	public void SwitchFile( MovieFile file )
	{
		if ( Session.Clip == file.Clip ) return;

		if ( Session.Clip == Session.Player.EmbeddedClip && Session.Clip?.TrackCount > 0 )
		{
			Dialog.AskConfirm( () =>
			{
				ConfirmedSwitchFile( file );
			}, question: "Switching to a clip resource will cause your embedded clip to be lost. Are you sure?" );
		}
		else
		{
			ConfirmedSwitchFile( file );
		}
	}

	private void ConfirmedSwitchFile( MovieFile file )
	{
		Session.Player.ReferencedClip = file;

		Switch( Session.Player );
	}

	public void SaveFileAs()
	{
		var extension = typeof(MovieFile).GetCustomAttribute<GameResourceAttribute>()!.Extension;

		var fd = new FileDialog( null );
		fd.Title = $"Save Clip As..";
		fd.Directory = Project.Current.GetAssetsPath();
		fd.DefaultSuffix = $".{extension}";
		fd.SetFindFile();
		fd.SetModeSave();
		fd.SetNameFilter( $"Movie Clip File (*.{extension})" );

		if ( !fd.Execute() )
			return;

		var sceneAsset = AssetSystem.CreateResource( extension, fd.SelectedFile );
		var file = new MovieFile { Clip = Session.Clip?.Clone() ?? new MovieClip() };

		sceneAsset.SaveToDisk( file );

		ConfirmedSwitchFile( file );
	}
}

