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
		TreeView.MultiSelect = true;
		TreeView.BodyContextMenu = OpenTreeViewContextMenu;
		TreeView.ItemSelected = x => _lastScene?.Scene?.EditLog( "Selection", this );
		TreeView.ItemsSelected = x => _lastScene?.Scene?.EditLog( "Selection", this );
		Footer = Layout.AddColumn();
		_lastScene = null;
		CheckForChanges();
	}

	void OpenTreeViewContextMenu()
	{
		var rootItem = TreeView.Items.FirstOrDefault();
		if ( rootItem is null ) return;

		if ( rootItem  is TreeNode node )
		{
			node.OnContextMenu();
		}
	}

	SceneEditorSession _lastScene;

	[EditorEvent.Frame]
	public void CheckForChanges()
	{
		var activeScene = SceneEditorSession.Active;

		if ( _lastScene == activeScene ) return;

		_lastScene = activeScene;

		Header.Clear( true );

		// treeview will clear the selection, so give it a new one to clear
		TreeView.Selection = new SelectionSystem();
		TreeView.Clear();

		if ( _lastScene is null )
			return;

		if ( _lastScene is not GameEditorSession && _lastScene.Scene is PrefabScene prefabScene )
		{
			var node = TreeView.AddItem( new PrefabNode( prefabScene ) );
			TreeView.Open( node );
		}
		else
		{
			var node = TreeView.AddItem( new SceneNode( _lastScene.Scene ) );
			TreeView.Open( node );
		}

		TreeView.Selection = activeScene.Selection;
	}
}

