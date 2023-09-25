
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
	Gizmo.Instance GizmoInstance;

	public SceneViewWidget( Widget parent ) : base( parent )
	{
		Camera = new SceneCamera();
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;

		Renderer = new NativeRenderingWidget( this );
		Renderer.Size = 200;
		Renderer.Camera = Camera;

		Layout = Layout.Column();
		Layout.Add( Renderer );

		GizmoInstance = new Gizmo.Instance();
		Camera.Worlds.Add( GizmoInstance.World );
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

		GizmoInstance.FirstPersonCamera( Camera, Renderer );
		GizmoInstance.UpdateInputs( Camera, Renderer );

		if ( activeScene is null )
			return;

		activeScene.SceneWorld.AmbientLightColor = Color.Black;

		using ( GizmoInstance.Push() )
		{
			Cursor = Gizmo.HasHovered ? CursorShape.Finger : CursorShape.Arrow;
			activeScene.DrawGizmos();
			activeScene.Tick();
			activeScene.PreRender();

			foreach ( var obj in activeScene.All )
			{
				using ( Gizmo.Scope( $"obj{obj.GetHashCode()}", obj.Transform.WithRotation( Rotation.Identity ) ) )
				{
					if ( Gizmo.Control.Position( "position", obj.Transform.Position, out var position, obj.Transform.Rotation ) )
					{
						obj.Transform = obj.Transform.WithPosition( position );
					}
				}
			}
		}
	}
}

