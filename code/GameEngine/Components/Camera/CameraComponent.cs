using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Linq;

[Title( "Camera" )]
[Category( "Camera" )]
[Icon( "videocam", "red", "white" )]
[EditorHandle( "materials/gizmo/camera.png" )]
public class CameraComponent : BaseComponent
{
	SceneCamera sceneCamera = new SceneCamera();

	[Property]
	public ClearFlags ClearFlags { get; set; } = ClearFlags.Color | ClearFlags.Stencil | ClearFlags.Depth;

	[Property]
	public Color BackgroundColor { get; set; } = "#557685";

	[Property]
	public float FieldOfView { get; set; } = 60;

	[Property]
	public float ZNear { get; set; } = 10;

	[Property]
	public float ZFar { get; set; } = 10000;

	public override void DrawGizmos()
	{
		if ( sceneCamera is null )
			return;

		using var scope = Gizmo.Scope( $"{GetHashCode()}" );

		Gizmo.Transform = Gizmo.Transform.WithScale( 1 );

		sceneCamera.Position = Vector3.Zero;
		sceneCamera.Rotation = Rotation.Identity;
		sceneCamera.FieldOfView = FieldOfView;
		sceneCamera.BackgroundColor = BackgroundColor;

		var cs = new Vector2( 1920, 1080 );

		var tl = sceneCamera.GetRay( new Vector3( 0, 0 ), cs );
		var tr = sceneCamera.GetRay( new Vector3( cs.x, 0 ), cs );
		var bl = sceneCamera.GetRay( new Vector3( 0, cs.y ), cs );
		var br = sceneCamera.GetRay( new Vector3( cs.x, cs.y ), cs );

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );

		Gizmo.Draw.Line( tl.Forward * ZNear, tl.Forward * ZFar );
		Gizmo.Draw.Line( tr.Forward * ZNear, tr.Forward * ZFar );
		Gizmo.Draw.Line( bl.Forward * ZNear, bl.Forward * ZFar );
		Gizmo.Draw.Line( br.Forward * ZNear, br.Forward * ZFar );

		Gizmo.Draw.Line( tl.Forward * ZNear, tr.Forward * ZNear );
		Gizmo.Draw.Line( tr.Forward * ZNear, br.Forward * ZNear );
		Gizmo.Draw.Line( br.Forward * ZNear, bl.Forward * ZNear );
		Gizmo.Draw.Line( bl.Forward * ZNear, tl.Forward * ZNear );

		Gizmo.Draw.Line( tl.Forward * ZFar, tr.Forward * ZFar );
		Gizmo.Draw.Line( tr.Forward * ZFar, br.Forward * ZFar );
		Gizmo.Draw.Line( br.Forward * ZFar, bl.Forward * ZFar );
		Gizmo.Draw.Line( bl.Forward * ZFar, tl.Forward * ZFar );
	}

	public void UpdateCamera( SceneCamera camera )
	{
		var scene = GameObject.Scene;
		if ( scene is null )
		{
			Log.Warning( $"Trying to update camera from {this} but has no scene" );
			return;
		}

		camera.World = scene.SceneWorld;
		camera.Worlds.Clear();
		camera.Worlds.Add( scene.DebugSceneWorld );
		camera.Position = Transform.Position;
		camera.Rotation = Transform.Rotation;
		camera.ZNear = ZNear;
		camera.ZFar = ZFar;
		camera.FieldOfView = FieldOfView;
		camera.BackgroundColor = BackgroundColor;

		camera.VolumetricFog.Enabled = true;
		camera.VolumetricFog.ContinuousMode = true;
		camera.VolumetricFog.DrawDistance = 4096;
		camera.VolumetricFog.FadeInStart = 64;
		camera.VolumetricFog.FadeInEnd = 256;
		camera.VolumetricFog.IndirectStrength = 1.0f;
		camera.VolumetricFog.Anisotropy = 1;
		camera.VolumetricFog.Scattering = 1.0f;

		// defaults - let components override
		camera.Tonemap.Enabled = false;
		camera.CubemapFog.Enabled = false;

		camera.OnRenderOverlay = () => OnCameraRenderOverlay( camera );

		foreach ( var c in GetComponents<ISceneCameraSetup>() )
		{
			c.SetupCamera( this, camera );
		}
	}

	private void OnCameraRenderOverlay( SceneCamera camera )
	{
		if ( Scene is null )
			return;

		Scene.OnRenderOverlayInternal( camera );
	}

	public interface ISceneCameraSetup
	{
		void SetupCamera( CameraComponent camera, SceneCamera sceneCamera );
	}

}
