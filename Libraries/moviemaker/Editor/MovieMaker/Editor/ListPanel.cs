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

	private readonly Label _projectTitle;

	public ListPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		TrackList = new TrackListWidget( this, session );
		Layout.Add( TrackList );

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

			var openMenu = menu.AddMenu( "Open Movie", "file_open" );

			var movies = ResourceLibrary.GetAll<MovieResource>().ToArray();

			openMenu.AddOptions( movies, x => $"{x.ResourcePath}:{resourceIcon}", Editor.SwitchResource );

			session.CreateImportMenu( menu );

			menu.AddSeparator();

			menu.AddOption( $"Save Movie", "save", parent.OnSave, shortcut: "CTRL+S" );

			var saveAsMenu = menu.AddMenu( $"Save Movie As..", "save_as" );

			var embed = saveAsMenu.AddOption( "Embedded", "attach_file", parent.SwitchToEmbedded );

			embed.Checkable = true;
			embed.Checked = session.Resource is EmbeddedMovieResource;
			embed.ToolTip = "Store the movie inside this Movie Player component, embedded in the current scene or prefab.";

			saveAsMenu.AddOption( "New Movie Resource", resourceIcon, parent.SaveFileAs );

			menu.AddSeparator();

			var playerMenu = menu.AddMenu( "Switch Movie Player", "switch_video" );

			foreach ( var player in session.Player.Scene.GetAllComponents<MoviePlayer>() )
			{
				var option = playerMenu.AddOption( player.GameObject.Name, "live_tv", () => Editor.Switch( player ) );

				option.Checkable = true;
				option.Checked = session.Player == player;
			}

			menu.OpenAt( fileGroup.ScreenRect.BottomLeft );
		} );

		fileAction.ToolTip = "File menu for opening, importing, or saving movie projects.";

		// File name label

		var resourceGroup = ToolBar.AddGroup( true );

		_projectTitle = resourceGroup.AddLabel( session.Player.GameObject.Name );

		_projectTitle.Alignment = TextFlag.Center;
		_projectTitle.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.TabBackground );
		Paint.DrawRect( LocalRect );
	}
}
