namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// A panel with a toolbar at the top.
/// </summary>
public class MovieEditorPanel : Widget
{
	public MovieEditor Editor { get; }
	public ToolBarWidget ToolBar { get; }

	public MovieEditorPanel( MovieEditor parent )
		: base( parent )
	{
		Editor = parent;
		Layout = Layout.Column();

		ToolBar = new ToolBarWidget( this );

		Layout.Add( ToolBar );
	}
}
