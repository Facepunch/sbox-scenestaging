
using Editor;
using Editor.PanelInspector;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using System.Linq;
using System;
using Sandbox;

[Dock( "Editor", "Scene", "grid_4x4" )]
public partial class SceneViewWidget : Widget
{
	NativeRenderingWidget Renderer;
	SceneCamera Camera;
	

	public SceneViewWidget( Widget parent ) : base( parent )
	{
		Camera = new SceneCamera();
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;

		Renderer = new NativeRenderingWidget( this );
		Renderer.Size = 200;
		Renderer.Camera = Camera;

		Layout = Layout.Column();
		Layout.Add( Renderer );

		
		Camera.Worlds.Add( EditorScene.GizmoInstance.World );
	}

	[EditorEvent.Frame]
	public void Ticker()
	{
		var activeScene = EditorScene.GetAppropriateScene();

		Camera.World = activeScene?.SceneWorld;
		Camera.ZNear = 1;
		Camera.ZFar = 10000;
		Camera.FieldOfView = 90;
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
		Camera.BackgroundColor = "#557685";

		EditorScene.GizmoInstance.FirstPersonCamera( Camera, Renderer );
		EditorScene.GizmoInstance.UpdateInputs( Camera, Renderer );

		if ( activeScene is null )
			return;

		activeScene.SceneWorld.AmbientLightColor = Color.Black;

		using ( EditorScene.GizmoInstance.Push() )
		{
			Cursor = Gizmo.HasHovered ? CursorShape.Finger : CursorShape.Arrow;
			activeScene.Tick();
			activeScene.PreRender();

			activeScene.DrawGizmos();

			if ( Gizmo.HasClicked && Gizmo.HasHovered )
			{
			
			}

			if ( Gizmo.HasClicked && !Gizmo.HasHovered )
			{
				Gizmo.Select();
			}
		}
	}
}

