
using Editor;
using Editor.PanelInspector;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using System.Linq;
using System;
using Sandbox;


[Dock( "Editor", "Hierachy", "list" )]
public partial class SceneTreeWidget : Widget
{
	TreeView TreeView;

	public SceneTreeWidget( Widget parent ) : base( parent )
	{
		Layout = Layout.Column();

		BuildUI();
	}

	[Event.Hotload]
	public void BuildUI()
	{
		Layout.Clear( true );
		TreeView = Layout.Add( new TreeView( this ), 1 );
		_lastScene = null;
		CheckForChanges(); 
	}
	 
	Scene _lastScene;

	[EditorEvent.Frame]
	public void CheckForChanges()
	{
	//	Log.Info( $"{_lastScene} / {Scene.Active} / {_lastScene == Scene.Active}" );

		if ( _lastScene == Scene.Active ) return;
		_lastScene = Scene.Active;

		TreeView.Clear();

		if ( _lastScene is not null )
		{
			var sceneNode = TreeView.AddItem( new SceneNode( _lastScene ) );
			TreeView.Open( sceneNode );
		}
	}
}

