using Sandbox.MovieMaker;
using System.Linq;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Panel containing the track list.
/// </summary>
public sealed class ListPanel : MovieEditorPanel
{
	public TrackListWidget TrackList { get; }

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

			menu.OpenAt( fileGroup.ScreenRect.BottomLeft );
		} );

		fileAction.ToolTip = "File menu for opening, importing, or saving movie projects.";

		// MoviePlayer selection

		var playerGroup = ToolBar.AddGroup( true );
		var playerCombo = playerGroup.Layout.Add( new PlayerComboBox( session ) );

		playerCombo.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;

		playerCombo.Bind( "Value" ).From(
			() => session.Player,
			value => session.Editor.Switch( value ) );
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.TabBackground );
		Paint.DrawRect( LocalRect );
	}
}

file class PlayerComboBox : IconComboBox<MoviePlayer?>
{
	private readonly Session _session;

	public PlayerComboBox( Session session )
	{
		_session = session;

		IconAspect = null;
	}

	protected override IEnumerable<MoviePlayer?> OnGetOptions() =>
		_session.Player.Scene.IsValid ? _session.Player.Scene.GetAllComponents<MoviePlayer>() : [];

	protected override string OnGetOptionTitle( MoviePlayer? option ) => option?.GameObject.Name ?? "None";

	protected override void OnPaintOptionIcon( MoviePlayer? option, Rect rect )
	{
		Paint.DrawText( rect, OnGetOptionTitle( option ) );
	}

	protected override void OnCreateMenu( Menu menu )
	{
		base.OnCreateMenu( menu );

		menu.AddSeparator();

		menu.AddOption( "Create New Movie Player", "live_tv", _session.Editor.CreateNewPlayer );
	}
}
