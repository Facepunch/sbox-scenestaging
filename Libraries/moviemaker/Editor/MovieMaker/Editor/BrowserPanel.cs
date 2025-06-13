using Sandbox.MovieMaker;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Editor.MovieMaker;

#nullable enable

public interface IBrowserPanelPage
{
	ToolBarItemDisplay Display { get; }
	bool Visible { get; set; }
}

file sealed class DummyPage( ToolBarItemDisplay display ) : Widget, IBrowserPanelPage
{
	public ToolBarItemDisplay Display => display;

	public bool Visible { get; set; }
}

/// <summary>
/// Panel containing pages for movie resources, movie players, and undo history.
/// </summary>
public sealed class BrowserPanel : MovieEditorPanel
{
	private const string PageCookieName = "moviemaker.browserpage";

	public MovieResourceListPage MovieList { get; }
	public MoviePlayerListPage PlayerList { get; }

	private readonly Label _pageTitle;

	private readonly ImmutableArray<IBrowserPanelPage> _pages;

	public BrowserPanel( MovieEditor parent, Session session )
		: base( parent )
	{
		_pages =
		[
			MovieList = new MovieResourceListPage( this, session ),
			PlayerList = new MoviePlayerListPage( this, session ),
			new HistoryPage( this, session )
		];

		var initialPageTypeName = EditorCookie.Get<string>( PageCookieName, null! );
		var initialPage = _pages.FirstOrDefault( x => x.GetType().Name == initialPageTypeName )
			?? _pages.First();

		MinimumWidth = 300;

		// Page title

		var resourceGroup = ToolBar.AddGroup( true );

		_pageTitle = resourceGroup.AddLabel( initialPage.Display.Title );

		_pageTitle.Alignment = TextFlag.Center;
		_pageTitle.HorizontalSizeMode = SizeMode.CanGrow | SizeMode.Expand;

		// Page list

		var pagesGroup = ToolBar.AddGroup( true );

		foreach ( var page in _pages )
		{
			pagesGroup.AddToggle( page.Display,
				() => page.Visible,
				_ =>
				{
					EditorCookie.Set( PageCookieName, page.GetType().Name );
					SetPage( page );
				} );
		}

		SetPage( initialPage );
	}

	public void SetPage( IBrowserPanelPage page )
	{
		foreach ( var widget in _pages )
		{
			widget.Visible = widget == page;
		}

		Layout.Clear( false );
		Layout.Add( ToolBar );
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

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.TabBackground );
		Paint.DrawRect( LocalRect );
	}
}
