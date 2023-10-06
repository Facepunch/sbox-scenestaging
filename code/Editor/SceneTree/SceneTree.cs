[Dock( "Editor", "Hierarchy", "list" )]
public partial class SceneTreeWidget : Widget
{
	TreeView TreeView;

	Layout Header;
	Layout Footer;

	public SceneTreeWidget( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();

		BuildUI();
	}

	[Event.Hotload]
	public void BuildUI()
	{
		Layout.Clear( true );
		Layout.Add( new OpenSceneList( this ) );
		Header = Layout.AddColumn();
		Header = Layout.AddColumn();
		TreeView = Layout.Add( new TreeView( this ), 1 );
		TreeView.Selection = EditorScene.Selection;
		Footer = Layout.AddColumn();
		_lastScene = null;
		CheckForChanges();
	}

	Scene _lastScene;

	[EditorEvent.Frame]
	public void CheckForChanges()
	{
		var activeScene = EditorScene.Active;

		if ( _lastScene == activeScene ) return;

		_lastScene = activeScene;

		Header.Clear( true );
		TreeView.Clear();

		if ( _lastScene is null )
			return;

		if ( _lastScene.IsEditor && _lastScene is PrefabScene prefabScene )
		{
			var node = TreeView.AddItem( new PrefabNode( prefabScene ) );
			TreeView.Open( node );

			return;
		}
		else
		{
			var node = TreeView.AddItem( new SceneNode( _lastScene ) );
			TreeView.Open( node );
		}
	}
}

