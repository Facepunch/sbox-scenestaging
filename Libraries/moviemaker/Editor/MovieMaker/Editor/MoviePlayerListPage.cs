using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed class MoviePlayerListPage : ListView, IListPanelPage
{
	public ToolBarItemDisplay Display { get; } = new( "Movie Players", "live_tv",
		"Lists movie playback components in the scene, so you can switch between them." );

	public Session Session { get; }

	public MoviePlayerListPage( ListPanel parent, Session session )
		: base( parent )
	{
		Session = session;
	}

	protected override void OnItemActivated( object item )
	{
		if ( item is not MoviePlayer player ) return;

		if ( Session.Player != player )
		{
			Session.Editor.Switch( player );
			Session.Editor.ListPanel?.OpenPlayerPage();
		}
	}

	[EditorEvent.Frame]
	private void Frame()
	{
		if ( !Visible ) return;

		var hash = new HashCode();

		foreach ( var player in Session.Player.Scene.GetAllComponents<MoviePlayer>() )
		{
			hash.Add( player.GameObject.Name );
		}

		if ( !SetContentHash( hash.ToHashCode() ) ) return;

		ItemSize = new Vector2( -1f, 32f );

		SetItems( Session.Player.Scene.GetAllComponents<MoviePlayer>() );
	}

	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not MoviePlayer player )
			return;

		var col = Theme.ControlText;

		if ( Paint.HasPressed ) col = Theme.Yellow;
		else if ( player == Session.Player ) col = Theme.Blue;
		else if ( Paint.HasMouseOver ) col = Theme.Green;

		if ( Paint.HasPressed || player == Session.Player || Paint.HasMouseOver )
		{
			Paint.ClearPen();
			Paint.SetBrush( col.WithAlpha( 0.1f ) );
			Paint.DrawRect( item.Rect.Shrink( 1 ), 6 );
		}

		Paint.SetPen( col );
		Paint.DrawIcon( new Rect( item.Rect.TopLeft, 32f ), "live_tv", 16f );
		Paint.DrawText( item.Rect.Shrink( 32f, 0f, 0f, 0f ), player.GameObject.Name, TextFlag.LeftCenter );

		var resourceName = player.Resource switch
		{
			EmbeddedMovieResource => "Embedded",
			MovieResource res => $"{res.ResourceName}.movie",
			_ => "None"
		};

		Paint.SetPen( col.Darken( 0.25f ) );
		Paint.SetFont( null, 8f );
		Paint.DrawText( item.Rect.Shrink( 0f, 0f, 8f, 0f ), resourceName, TextFlag.RightCenter );
	}
}
