using Editor.EntityPrefabEditor;
using Sandbox;
using System.Collections.Generic;

namespace Editor.Inspectors;

[CanEdit( "asset:scene" )]
public class SceneFileInspector : Widget
{
	public SceneFileInspector( Widget parent ) : base( parent )
	{
		// edit scene info ?
		// show number of game objects ?
	}
}
