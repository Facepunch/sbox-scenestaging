using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

public interface IListPanelPage
{
	ToolBarItemDisplay Display { get; }
	bool Visible { get; set; }
}

file sealed class DummyPage( ToolBarItemDisplay display ) : Widget, IListPanelPage
{
	public ToolBarItemDisplay Display => display;

	public bool Visible { get; set; }
}

/// <summary>
/// Panel containing the track list.
/// </summary>
public sealed class ListPanel : MovieEditorPanel
{
	private const string PageCookieName = "moviemaker.listpage";

	public const float TitleHeight = 32f;

	private readonly Label _pageTitle;

	public TrackListPage TrackList { get; }
	public MovieResourceListPage MovieList { get; }
	public MoviePlayerListPage PlayerList { get; }

	private readonly ImmutableArray<IListPanelPage> _pages;

	public ListPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		_pages =
		[
			TrackList = new TrackListPage( this, session ),
			MovieList = new MovieResourceListPage( this, session ),
			PlayerList = new MoviePlayerListPage( this, session ),
			new HistoryPage( this, session )
		];

		_pageTitle = new Label.Header( this );
		_pageTitle.Alignment = TextFlag.Center;
		_pageTitle.FixedHeight = TitleHeight;
		_pageTitle.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;

		var initialPageTypeName = Cookie.Get<string>( PageCookieName, null! );
		var initialPage = _pages.FirstOrDefault( x => x.GetType().Name == initialPageTypeName )
			?? _pages.First();

		SetPage( initialPage );

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

			menu.OpenAt( fileGroup.ScreenRect.BottomLeft );
		} );

		fileAction.ToolTip = "File menu for opening, importing, or saving movie projects.";

		foreach ( var page in _pages )
		{
			fileGroup.AddToggle( page.Display,
				() => page.Visible,
				_ =>
				{
					Cookie.Set( PageCookieName, page.GetType().Name );
					SetPage( page );
				} );
		}

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

	public void SetPage( IListPanelPage page )
	{
		foreach ( var widget in _pages )
		{
			widget.Visible = widget == page;
		}

		Layout.Clear( false );
		Layout.Add( ToolBar );
		Layout.Add( _pageTitle );
		Layout.Add( (Widget)page );

		_pageTitle.Text = page.Display.Title;
	}

	private static string GetFullPath( Session session )
	{
		var name = session.Resource is MovieResource res ? res.ResourceName.ToTitleCase() : "Embedded";

		return session.Parent is { } parent
			? $"{GetFullPath( parent )} → {name}"
			: name;
	}
}
