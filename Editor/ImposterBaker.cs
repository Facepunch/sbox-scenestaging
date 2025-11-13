using Sandbox;
using SceneStaging;

namespace Editor;

/// <summary>
/// Simple imposter baker - renders a prefab from a camera and saves as PNG.
/// </summary>
public static class ImposterBaker
{
	/// <summary>
	/// Bakes a simple imposter from a prefab asset.
	/// </summary>
	public static void BakeImposter( Asset prefabAsset, int resolution = 512 )
	{
		// Load the prefab
		var prefabFile = prefabAsset.LoadResource<PrefabFile>();
		if ( prefabFile == null )
		{
			Log.Error( $"Failed to load prefab: {prefabAsset.Name}" );
			return;
		}

		// Create a temporary scene
		var scene = new Scene();
		using ( scene.Push() )
		{
			// Instantiate the prefab
			var prefabScene = SceneUtility.GetPrefabScene( prefabFile );
			var prefabRoot = prefabScene.Clone();
			prefabScene.EditorTick( Time.Now, Time.Delta ); // Ensure any editor-time logic runs
			
			var bounds = prefabRoot.GetBounds();
			var center = bounds.Center;
			var distance = bounds.Size.Length * 2f;

			// Setup camera
			var cameraRotation = Rotation.From( 30f, 45f, 0f );

			using var camera = new SceneCamera( "Imposter Baker" );
			camera.World = scene.SceneWorld;
			camera.Position = center - cameraRotation.Forward * distance;
			camera.Rotation = cameraRotation;
			camera.FieldOfView = 30f;
			camera.BackgroundColor = Color.Transparent;

			var go = scene.CreateObject();
			go.Name = "Directional Light";
			go.WorldRotation = Rotation.From( 90, 0, 0 ); // Shines downward
			var light = go.Components.Create<DirectionalLight>();
			light.LightColor = Color.White;
			scene.SceneWorld.AmbientLightColor = "#557685"; // Soft blue-grey ambience

			scene.GameTick( 0.1f );
			scene.GameTick( 0.1f );

			// Render to pixmap
			var pixmap = new Pixmap( resolution, resolution );
			camera.RenderToPixmap( pixmap );

			// Save as PNG
			var outputPath = System.IO.Path.ChangeExtension( prefabAsset.AbsolutePath, ".png" );
			pixmap.SavePng( outputPath );

			Log.Info( $"Saved imposter to: {outputPath}" );
		}
	}
}
