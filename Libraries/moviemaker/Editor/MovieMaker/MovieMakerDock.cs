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
	private void Build()
	{
		Layout.Clear( true );
		Layout.Add( new MovieEditor( this ) );
	}

	private int _titleHash;

	[EditorEvent.Frame]
	private void Frame()
	{
		UpdateTitle();
	}

	private void UpdateTitle()
	{
		var titleHash = HashCode.Combine( Session.Current?.HasUnsavedChanges );

		if ( _titleHash != titleHash )
		{
			if ( Session.Current is not { Player: { } player } session )
			{
				WindowTitle = "Movie Maker";
				return;
			}

			WindowTitle = session.HasUnsavedChanges
				? "Movie Maker*"
				: "Movie Maker";
		}
	}
}

