namespace Editor.MovieMaker;


[Dock( "Editor", "Movie Maker", "movie_creation" )]
public class MovieMakerDock : Widget
{
	public MovieMakerDock( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();
		FocusMode = FocusMode.TabOrClickOrWheel;

		Build();
	}

	[EditorEvent.Hotload]
	void Build()
	{
		Layout.Clear( true );
		Layout.Add( new MovieEditor( this ) );
	}
}

