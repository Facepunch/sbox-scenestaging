using Editor.Assets;
using System;

public static class SceneThumbnailRenderer
{
	[Asset.ThumbnailRenderer]
	public static Pixmap RenderAssetThumbnail( Asset asset )
	{
		if ( asset.AssetType.FileExtension == "scene" )
		{
			var file = asset.LoadResource<SceneFile>();
			if ( file is null ) return null;

			return RenderFor( file );
		}

		if ( asset.AssetType.FileExtension == "object" )
		{
			var file = asset.LoadResource<PrefabFile>();
			if ( file is null ) return null;

			return RenderFor( file );
		}

		//AssetVideo v = CreateForAsset( asset );

		// unsupported
		//if ( v is null )
		//	return null;

		//v.InitializeScene();
		//v.InitializeAsset();
		//v.UpdateScene( 0, 0.1f );

		//var pix = new Pixmap( 256, 256 );

		//v.Camera.RenderToPixmap( pix );

		return null;
	}

	private static Pixmap RenderFor( PrefabFile file )
	{
		var pix = new Pixmap( 256, 256 );

		var scene = PrefabScene.Create();

		using ( scene.Push() )
		{
			scene.Load( file );

			TryAddDefaultLighting( scene );
			RenderScene( pix, scene );
		}

		return pix;
	}

	private static Pixmap RenderFor( SceneFile file )
	{
		// rendering scenes in 16:9 
		var pix = new Pixmap( 512, 288 );

		var scene = Scene.CreateEditorScene();

		using ( scene.Push() )
		{
			scene.Load( file );

			TryAddDefaultLighting( scene );
			RenderScene( pix, scene );
		}

		return pix;
	}

	static void TryAddDefaultLighting( Scene scene )
	{
		if ( scene.GetComponent<DirectionalLightComponent>( false, true ) is not null ) return;
		if ( scene.GetComponent<SpotLightComponent>( false, true ) is not null ) return;
		if ( scene.GetComponent<PointLightComponent>( false, true ) is not null ) return;

		var go = scene.CreateObject();
		go.Name = "Directional Light";

		go.Transform.Rotation = Rotation.From( 90, 0, 0 );
		var light = go.AddComponent<DirectionalLightComponent>();
		light.LightColor = Color.White;
		light.SkyColor = "#557685";
	}

	private static void RenderScene( Pixmap pix, Scene scene )
	{
		var camera = new SceneCamera();
		camera.World = scene.SceneWorld;
		camera.ZNear = 1;
		camera.ZFar = 50000;
		camera.FieldOfView = 80;
		camera.ClearFlags = ClearFlags.Color | ClearFlags.Depth | ClearFlags.Stencil;

		var cam = scene.FindAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam is not null )
		{
			camera.Position = cam.Transform.Position;
			camera.Rotation = cam.Transform.Rotation;
			camera.FieldOfView = cam.FieldOfView;
			camera.BackgroundColor = cam.BackgroundColor;
		}
		else
		{
			camera.Position = Vector3.Backward * 200 + Vector3.Up * 200 + Vector3.Left * 100;
			camera.Rotation = Rotation.LookAt( -camera.Position );
			camera.BackgroundColor = "#557685";

			var bounds = scene.GetBounds();

			var distance = MathX.SphereCameraDistance( bounds.Size.Length, camera.FieldOfView );
			camera.Position = bounds.Center + distance * camera.Rotation.Backward;
		}

		camera.RenderToPixmap( pix );
	}
}
