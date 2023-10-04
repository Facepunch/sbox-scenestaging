
using Editor;
using Editor.PanelInspector;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using System.Linq;
using System;
using Sandbox;


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
		var activeScene = EditorScene.GetAppropriateScene();

		if ( _lastScene == activeScene ) return;

		_lastScene = activeScene;

		Header.Clear( true );
		TreeView.Clear();

		if ( _lastScene is null )
			return;

		if (  _lastScene.IsEditor && _lastScene.SourcePrefabFile is not null )
		{
			Header.Add( new Button( _lastScene.SourcePrefabFile.ResourceName ) { Clicked = () => { EditorScene.ClosePrefabScene(); } } );

			var node = TreeView.AddItem( new PrefabNode( _lastScene.All.First() ) );
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

