namespace Editor.MovieMaker;

#nullable enable

public sealed class HistoryPage : Widget, IListPanelPage
{
	public ToolBarItemDisplay Display { get; } = new( "History", "history",
		"Lists changes made in this editor session, and lets you revert or reapply them." );

	public Session Session { get; }

	public HistoryPage( ListPanel parent, Session session )
		: base( parent )
	{
		Session = session;
	}
}

