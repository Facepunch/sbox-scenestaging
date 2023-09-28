using Editor;
using Sandbox;
using System.Linq;

public static class EditorScene
{
	public static Scene Active { get; set; }
	public static Scene[] All { get; set; }

	internal static Gizmo.Instance GizmoInstance { get; private set; }

	public static SelectionSystem Selection => GizmoInstance.Selection;

	static EditorScene()
	{
		GizmoInstance = new Gizmo.Instance();

		Active = new Scene();
		Active.Name = "Untitled Scene";
		Active.IsEditor = true;

		All = new[] { Active };

		{
			var go = Active.CreateObject();
			go.Name = "Main Camera";
			go.Transform = new Transform( Vector3.Up * 100 + Vector3.Backward * 300 );
			go.AddComponent<CameraComponent>();
		}

		{
			var go = Active.CreateObject();
			go.Name = "Directional Light";
			go.Transform = new Transform( Vector3.Up * 200, Rotation.From( 80, 45, 0 ) );
			go.AddComponent<DirectionalLightComponent>();
		}

		for ( int i = 0; i < 1000; i++ )
		{
			var go = Active.CreateObject();
			go.Name = "Sphere Object";
			go.Transform = new Transform( Vector3.Random * 1000 );
			var model = go.AddComponent<ModelComponent>();
			model.Model = Model.Load( "models/dev/sphere.vmdl" );
		}
	}

	[EditorForAssetType( "scene" )]
	public static void LoadFromScene( SceneSource resource )
	{
		Active = new Scene();
		Active.Name = resource.ResourceName.ToTitleCase();
		Active.IsEditor = true;

		Active.Load( resource );

		All = new[] { Active };
	}

	public static Scene GetAppropriateScene()
	{
		if ( GameManager.IsPlaying ) return Scene.Active;
		return EditorScene.Active;
	}

	public static void Play()
	{
		var current = EditorScene.Active.Save();

		GameManager.IsPlaying = true;

		Scene.Active = new Scene();
		Scene.Active.Load( current );

		Camera.Main.World = Scene.Active.SceneWorld;
		Camera.Main.Worlds.Clear();
		Camera.Main.Worlds.Add( Scene.Active.DebugSceneWorld );
		Camera.Main.Position = 0;
		Camera.Main.ZNear = 1;
		Camera.Main.Tonemap.Enabled = true;
		Camera.Main.Tonemap.MinExposure = 0.1f;
		Camera.Main.Tonemap.MaxExposure = 2.0f;
		Camera.Main.Tonemap.Rate = 1.0f;
		Camera.Main.Tonemap.Fade = 1.0f;

		EditorWindow.DockManager.RaiseDock( "GameFrame" );
	}

	public static void Stop()
	{
		GameManager.IsPlaying = false;

		EditorWindow.DockManager.RaiseDock( "Scene" );

		UpdateGameCamera();

	}

	/// <summary>
	/// Called once a frame to keep the game camera in sync with the main camera in the editor scene
	/// </summary>
	[EditorEvent.Frame]
	public static void UpdateGameCamera()
	{
		if ( GameManager.IsPlaying ) return;
		if ( EditorScene.Active is null ) return;
		if ( Camera.Main is null ) return;

		var camera = EditorScene.Active.FindAllComponents<CameraComponent>( false ).FirstOrDefault();

		if ( camera is not null )
		{
			camera.UpdateCamera( Camera.Main );
		}
	}
}
