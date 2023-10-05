
using Editor;
using Editor.PanelInspector;
using Sandbox.Diagnostics;
using Sandbox.Internal;
using System.Linq;
using System;
using Sandbox;
using System.Threading.Tasks;

[Dock( "Editor", "Scene", "grid_4x4" )]
public partial class SceneViewWidget : Widget
{
	public static SceneViewWidget Current { get; private set; }

	NativeRenderingWidget Renderer;
	public SceneCamera Camera;
	SceneViewToolbar SceneToolbar;

	public SceneViewWidget( Widget parent ) : base( parent )
	{
		Camera = new SceneCamera();
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;

		Renderer = new NativeRenderingWidget( this );
		Renderer.Size = 200;
		Renderer.Camera = Camera;

		Layout = Layout.Column();
		SceneToolbar = Layout.Add( new SceneViewToolbar( this ) );
		Layout.Add( Renderer );

		Camera.Worlds.Add( EditorScene.GizmoInstance.World );
	}

	int selectionHash = 0;

	Vector3? cameraTargetPosition;
	Vector3 cameraVelocity;

	[EditorEvent.Frame]
	public void Frame()
	{
		if ( selectionHash != EditorScene.Selection.GetHashCode() )
		{
			// todo - multiselect
			EditorUtility.InspectorObject = EditorScene.Selection.LastOrDefault();
			selectionHash = EditorScene.Selection.GetHashCode();
		}

		if ( !Visible )
			return;

		Current = this;

		var activeScene = EditorScene.GetAppropriateScene();

		Camera.World = activeScene?.SceneWorld;
		Camera.ZNear = EditorScene.GizmoInstance.Settings.CameraZNear;
		Camera.ZFar = EditorScene.GizmoInstance.Settings.CameraZFar;
		Camera.FieldOfView = EditorScene.GizmoInstance.Settings.CameraFieldOfView;
		Camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;
		Camera.BackgroundColor = "#557685";

		SceneToolbar.SceneInstance = EditorScene.GizmoInstance;

		if ( cameraTargetPosition is not null )
		{
			var pos = Vector3.SmoothDamp( Camera.Position, cameraTargetPosition.Value, ref cameraVelocity, 0.3f, RealTime.Delta );
			Camera.Position = pos;

			if ( cameraTargetPosition.Value.Distance( pos ) < 0.1f )
			{
				cameraTargetPosition = null;
			}
		}

		if( EditorScene.GizmoInstance.FirstPersonCamera( Camera, Renderer ) )
		{
			cameraTargetPosition = null;
		}

		EditorScene.GizmoInstance.UpdateInputs( Camera, Renderer );

		if ( activeScene is null )
			return;

		var sceneCamera = activeScene.FindAllComponents<CameraComponent>().FirstOrDefault();

		activeScene.SceneWorld.AmbientLightColor = Color.Black;

		if ( sceneCamera is not null )
		{
			Camera.BackgroundColor = sceneCamera.BackgroundColor;
		}

		using ( EditorScene.GizmoInstance.Push() )
		{
			Cursor = Gizmo.HasHovered ? CursorShape.Finger : CursorShape.Arrow;
			
			// pump the loop if they're not pumping it
			if ( !GameManager.IsPlaying )
			{
				activeScene.Tick();
			}

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

	[Event( "scene.open" )]
	public void SceneOpened()
	{
		var activeScene = EditorScene.GetAppropriateScene();
		if ( activeScene is null )
			return;

		// ideally we should allow multiple scene windows
		// and we should be saving the last camera setup per scene, per camera
		// and then we could restore them here.

		var cam = activeScene.FindAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam is not null )
		{
			Camera.Position = cam.GameObject.WorldTransform.Position;
			Camera.Rotation = cam.GameObject.WorldTransform.Rotation;
		}
		else
		{
			Camera.Position = Vector3.Backward * 2000 + Vector3.Up * 2000 + Vector3.Left * 2000;
			Camera.Rotation = Rotation.LookAt( -Camera.Position );

			var bbox = activeScene.GetBounds();

			FrameOn( bbox );
		}
	}


	[Event( "scene.frame" )]
	public void FrameOn( BBox target )
	{
		var distance = MathX.SphereCameraDistance( target.Size.Length, Camera.FieldOfView ) * 1.0f;
		var targetPos = target.Center + distance * Camera.Rotation.Backward;

		if ( Camera.Position.Distance( targetPos ) < 1.0f )
		{
			cameraTargetPosition = target.Center + distance * 2.0f * Camera.Rotation.Backward;
		}
		else
		{
			cameraTargetPosition = targetPos;
		}
	}
}

public class SceneViewToolbar : SceneToolbar
{
	public SceneViewToolbar( Widget parent) : base( parent )
	{

	}

	public override void BuildToolbar()
	{
		AddGizmoModes();
		AddSeparator();
		AddCameraDropdown();
		AddSeparator();
		AddAdvancedDropdown();
		
	}
}
