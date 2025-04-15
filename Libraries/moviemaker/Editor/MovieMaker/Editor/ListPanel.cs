using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Panel containing the track list.
/// </summary>
public sealed class ListPanel : MovieEditorPanel
{
	public TrackListWidget TrackList { get; }
	public Widget History { get; }
	public Widget Movies { get; }
	public Widget Players { get; }

	private readonly ImmutableArray<Widget> _pages;

	public ListPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		TrackList = new TrackListWidget( this, session );
		History = new Widget( this ) { Visible = false };
		Movies = new Widget( this ) { Visible = false };
		Players = new Widget( this ) { Visible = false };

		Layout.Add( TrackList );
		Layout.Add( History );
		Layout.Add( Movies );
		Layout.Add( Players );

		_pages = [TrackList, History, Movies, Players];

		MinimumWidth = 300;

		// File menu

		var fileGroup = ToolBar.AddGroup( true );
		var resourceIcon = typeof( MovieResource ).GetCustomAttribute<GameResourceAttribute>()!.Icon;

		var fileDisplay = new ToolBarItemDisplay( "File", "folder", "Actions for saving / loading / importing movies, or switching player components." );
		var fileAction = fileGroup.AddAction( fileDisplay, () =>
		{
			var menu = new Menu();

			menu.AddHeading( "File" );

			menu.AddOption( "New Movie", "note_add", Editor.SwitchToNewEmbedded );

			menu.AddSeparator();

			var openMenu = menu.AddMenu( "Open Movie", "folder_open" );

			var movies = ResourceLibrary.GetAll<MovieResource>().ToArray();

			openMenu.AddOptions( movies, x => $"{x.ResourcePath}:{resourceIcon}", Editor.SwitchResource );

			var importMenu = menu.AddMenu( "Import Movie", "folder_open" );

			importMenu.AddOptions( movies, x => $"{x.ResourcePath}:{resourceIcon}", x =>
			{
				session.GetOrCreateTrack( x );
				session.TrackList.Update();
			} );

			menu.AddSeparator();

			menu.AddOption( $"Save Movie", "save", parent.OnSave, shortcut: "CTRL+S" );

			var saveAsMenu = menu.AddMenu( $"Save Movie As..", "save_as" );

			var embed = saveAsMenu.AddOption( "Embedded", "attach_file", parent.SwitchToEmbedded );

			embed.Checkable = true;
			embed.Checked = session.Resource is EmbeddedMovieResource;
			embed.ToolTip = "Store the movie inside this Movie Player component, embedded in the current scene or prefab.";

			saveAsMenu.AddOption( "New Movie Resource", resourceIcon, parent.SaveFileAs );

			menu.AddSeparator();

			var playerMenu = menu.AddMenu( "Select Player", "movie" );
			var scene = SceneEditorSession.Active?.Scene;
			var players = scene?.GetAllComponents<MoviePlayer>() ?? [];

			foreach ( var player in players )
			{
				var option = playerMenu.AddOption( player.GameObject.Name, "movie", () => Editor.Switch( player ) );

				option.Checkable = true;
				option.Checked = session.Player == player;
			}

			playerMenu.AddOption( "Create New..", "movie_filter", Editor.CreateNewPlayer );

			menu.OpenAt( fileGroup.ScreenRect.BottomLeft );
		} );

		fileGroup.AddToggle(
			new ToolBarItemDisplay( "Track List", "list_alt",
				"Lists tracks in the current movie, and allows you to add or remove them." ),
			() => TrackList.Visible,
			value => SetPage( TrackList ) );

		fileGroup.AddToggle(
			new ToolBarItemDisplay( "History", "history",
				"Lists changes made in this editor session, and lets you revert or reapply them." ),
			() => History.Visible,
			value => SetPage( History ) );

		fileGroup.AddToggle(
			new ToolBarItemDisplay( "Movies", "video_library",
				"Lists movie clips in the current project, letting you load or import them." ),
			() => Movies.Visible,
			value => SetPage( Movies ) );

		fileGroup.AddToggle(
			new ToolBarItemDisplay( "Players", "movie",
				"Lists movie playback components in the scene, so you can switch between them." ),
			() => Players.Visible,
			value => SetPage( Players ) );

		fileAction.ToolTip = "File menu for opening, importing, or saving movie projects.";

		// File name label

		var resourceGroup = ToolBar.AddGroup( true );

		var label = resourceGroup.AddLabel( GetFullPath( session ) );

		label.Alignment = TextFlag.Center;
		label.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		label.ToolTip = session.Resource is MovieResource res ? res.ResourcePath : "";

		// Navigation buttons

		var navigateGroup = ToolBar.AddGroup( true );
		var backDisplay = new ToolBarItemDisplay( "Back", "arrow_back", "Return to the parent Movie project." );

		navigateGroup.AddAction( backDisplay, parent.ExitSequence,
			() => parent.Session?.Parent is not null );
	}

	private void SetPage( Widget page )
	{
		foreach ( var widget in _pages )
		{
			widget.Visible = widget == page;
		}
	}

	private static string GetFullPath( Session session )
	{
		var name = session.Resource is MovieResource res ? res.ResourceName.ToTitleCase() : "Embedded";

		return session.Parent is { } parent
			? $"{GetFullPath( parent )} → {name}"
			: name;
	}
}
