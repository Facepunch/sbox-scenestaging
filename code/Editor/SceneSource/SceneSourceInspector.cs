using Editor.EntityPrefabEditor;
using Sandbox;
using System.Collections.Generic;

namespace Editor.Inspectors;

[CanEdit( "asset:scene" )]
public class SceneSourceInspector : Widget
{
	public SceneSourceInspector( Widget parent ) : base( parent )
	{
		// edit scene info ?
		// show number of game objects ?
	}
}
