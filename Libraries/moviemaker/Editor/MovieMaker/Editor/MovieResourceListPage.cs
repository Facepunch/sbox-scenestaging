using System.Text.Json;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed class MovieResourceListPage : ListView, IListPanelPage
{
	public ToolBarItemDisplay Display { get; } = new( "Movie Clips", "movie",
		"Lists movie clips in the current project, letting you load or import them." );

	public Session Session { get; }

	public MovieResourceListPage( ListPanel parent, Session session )
		: base( parent)
	{
		Session = session;

		ItemSize = new Vector2( 128f, 64f );

		IsDraggable = true;

		CheckItems();
	}

	protected override void OnVisibilityChanged( bool visible )
	{
		base.OnVisibilityChanged( visible );

		if ( visible )
		{
			CheckItems();
		}
	}

	[Event( "assetsystem.changes" )]
	private void CheckItems()
	{
		if ( !Visible ) return;

		var hash = new HashCode();

		foreach ( var movie in ResourceLibrary.GetAll<MovieResource>() )
		{
			hash.Add( movie.ResourceId );
		}

		if ( !SetContentHash( hash.ToHashCode() ) ) return;

		SetItems( ResourceLibrary.GetAll<MovieResource>() );
	}

	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not MovieResource resource ) return;

		var col = Color.White.WithAlpha( 0.7f );

		if ( resource == Session.Resource )
		{
			col = Theme.Blue;
		}
		else if ( Paint.HasPressed ) col = Theme.Yellow;
		else if ( Paint.HasSelected ) col = Theme.Blue;
		else if ( Paint.HasMouseOver ) col = Theme.Green;

		Paint.ClearPen();
		Paint.SetBrush( col.WithAlpha( 0.1f ) );
		Paint.DrawRect( item.Rect.Shrink( 1 ), 3 );

		Paint.SetPen( col );
		Paint.DrawText( item.Rect.Shrink( 8f, 4f, 8f, 24f ), resource.ResourceName.ToTitleCase(), TextFlag.Center | TextFlag.WordWrap );

		var duration = resource.Compiled?.Duration
			?? resource.EditorData?["Duration"]?.Deserialize<MovieTime>( EditorJsonOptions )
			?? MovieTime.Zero;

		var trackCount = resource.Compiled?.Tracks.Length
			?? resource.EditorData?["Tracks"]?.AsObject().Count
			?? 0;

		Paint.SetPen( col.WithAlpha( 0.5f ) );
		Paint.DrawText( item.Rect.Shrink( 8f, 4f ), duration.ToString(), TextFlag.LeftBottom );
		Paint.DrawText( item.Rect.Shrink( 8f, 4f ), $"{trackCount} Tracks", TextFlag.RightBottom );
	}

	protected override string GetTooltip( object obj )
	{
		if ( obj is not MovieResource resource ) return "";

		return resource.ResourcePath;
	}

	protected override void OnItemActivated( object item )
	{
		if ( item is not MovieResource resource ) return;

		Session.Editor.SwitchResource( resource );
	}

	protected override bool OnDragItem( VirtualWidget item )
	{
		if ( item.Object is not MovieResource resource ) return false;
		if ( !Session.CanReferenceMovie( resource ) ) return false;

		var asset = AssetSystem.FindByPath( resource.ResourcePath );
		var drag = new Drag( this ) { Data =
		{
			Text = resource.ResourcePath,
			Url = new System.Uri( $"file://{asset.AbsolutePath}" )
		} };

		drag.Execute();

		return true;
	}
}
