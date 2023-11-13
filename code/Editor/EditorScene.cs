using System;
using System.Threading.Tasks;

public static class EditorScene
{
	public static string PlayMode { get; set; } = "scene";

	internal static Gizmo.Instance GizmoInstance { get; private set; } = new Gizmo.Instance();

	public static SelectionSystem Selection => SceneEditorSession.Active?.Selection;

	public static string LastOpenedScene
	{
		get => ProjectCookie?.Get<string>( "scene.lastopened", null );
		set => ProjectCookie?.Set( "scene.lastopened", value );
	}

	[Event( "game.loaded" )]
	public static async void GameStarted( Package package )
	{
		var resource = ResourceLibrary.Get<SceneFile>( LastOpenedScene );
		if ( resource != null )
		{
			await LoadFromScene( resource );
		}

		// If we just started up, and there's no scenes
		// create a new, blank one.
		if ( SceneEditorSession.All.Count == 0 )
		{
			NewScene();
		}
	}

	public static void NewScene()
	{
		var scene = Scene.CreateEditorScene();
		scene.Name = "Untitled Scene";

		// create default scene

		{
			var go = scene.CreateObject();
			go.Name = "Main Camera";
			go.Transform.Local = new Transform( Vector3.Up * 100 + Vector3.Backward * 300 );
			go.AddComponent<CameraComponent>();
		}

		{
			var go = scene.CreateObject();
			go.Name = "Directional Light";
			go.Transform.Local = new Transform( Vector3.Up * 200, Rotation.From( 80, 45, 0 ) );
			go.AddComponent<DirectionalLightComponent>();
		}

		var newSession = new SceneEditorSession( scene );
		newSession.MakeActive();
		newSession.InitializeCamera();

		EditorEvent.Run( "scene.open" );
	}

	static SceneEditorSession previousActiveScene;

	public static void Play()
	{
		GameManager.IsPlaying = true;

		var activeSession = SceneEditorSession.Active;

		if ( PlayMode == "scene" )
		{
			// can't play prefabs
			if ( activeSession.Scene is PrefabScene )
			{
				GameManager.IsPlaying = false;
				return;
			}

			var current = activeSession.Scene.Save();

			GameManager.ActiveScene = new Scene();
			GameManager.ActiveScene.Load( current );
		}
		else
		{
			Program.Main();
		}

		// switch editor to active scene
		previousActiveScene = activeSession;

		var gameSession = new GameEditorSession( GameManager.ActiveScene );
		gameSession.MakeActive();

		if ( activeSession is not null )
		{
			gameSession.CameraPosition = activeSession.CameraPosition;
			gameSession.CameraRotation = activeSession.CameraRotation;
		}

		Camera.Main.World = GameManager.ActiveScene.SceneWorld;
		Camera.Main.Worlds.Clear();
		Camera.Main.Worlds.Add( GameManager.ActiveScene.DebugSceneWorld );

		EditorWindow.DockManager.RaiseDock( "GameFrame" );
		
		EditorEvent.Run("scene.play");
	}

	public static void Stop()
	{
		GameManager.IsPlaying = false;

		GameManager.ActiveScene.Clear();
		GameManager.ActiveScene = null;

		GameEditorSession.CloseAll();

		if ( SceneEditorSession.All.Contains( previousActiveScene ) )
		{
			previousActiveScene.MakeActive();
		}
		else
		{
			SceneEditorSession.All.FirstOrDefault()?.MakeActive();
		}

		EditorWindow.DockManager.RaiseDock( "Scene" );
		SceneEditorTick();
		
		EditorEvent.Run("scene.stop");
	}

	/// <summary>
	/// Called once a frame to keep the game camera in sync with the main camera in the editor scene
	/// </summary>
	[EditorEvent.Frame]
	public static void SceneEditorTick()
	{
		if ( SceneEditorSession.Active is null ) return;
		if ( Camera.Main is null ) return;

		SceneEditorSession.Tick();
	}

	/// <summary>
	/// This is called when the user wants to open a new scene
	/// </summary>

	[EditorForAssetType( "scene" )]
	public static async Task LoadFromScene( SceneFile resource )
	{
		Assert.NotNull( resource, "SceneFile should not be null" );
		
		LastOpenedScene = resource.ResourcePath;

		var asset = AssetSystem.FindByPath( resource.ResourcePath );
		asset?.RecordOpened();

		if ( !await CloudAsset.Install( "Loading Scene..", resource.GetReferencedPackages() ) )
			return;

		SceneEditorSession session = SceneEditorSession.All.Where( x => x.Scene.Source == resource ).FirstOrDefault();
		if ( session is not null )
		{
			session.MakeActive();
			return;
		}

		var openingScene = Scene.CreateEditorScene();
		using ( openingScene.Push() )
		{
			openingScene.Name = resource.ResourceName.ToTitleCase();
			openingScene.Load( resource );

			session = new SceneEditorSession( openingScene );
			session.MakeActive();
			session.InitializeCamera();

			EditorEvent.Run( "scene.open" );
		}
	}

	/// <summary>
	/// This is called when the user wants to open a new scene
	/// </summary>

	[EditorForAssetType( PrefabFile.FileExtension )]
	public static void LoadFromPrefab( PrefabFile resource )
	{
		Assert.NotNull( resource, "PrefabFile should not be null" );

		var asset = AssetSystem.FindByPath( resource.ResourcePath );
		asset?.RecordOpened();

		PrefabEditorSession session = SceneEditorSession.All.OfType<PrefabEditorSession>().Where( x => x.Scene.Source == resource ).FirstOrDefault();
		if ( session is not null )
		{
			session.MakeActive();
			return;
		}
		// add default lighting
		//new SceneSunLight( prefabScene.SceneWorld, Rotation.From( 80, 45, 0 ), Color.White * 0.5f );

		var prefabScene = PrefabScene.Create();
		using ( prefabScene.Push() )
		{
			prefabScene.Name = resource.ResourceName.ToTitleCase();
			prefabScene.Load( resource );

			session = new PrefabEditorSession( prefabScene );
			session.MakeActive();
			session.InitializeCamera();

			EditorEvent.Run( "scene.open" );
		}
	}

	static void UpdatePrefabsInScene( Scene scene, PrefabFile prefab )
	{
		using var activeScope = SceneUtility.DeferInitializationScope( "Update Prefabs" );
		var changedPath = prefab.ResourcePath;

		using ( scene.Push() )
		{
			foreach ( var obj in scene.GetAllObjects( false ) )
			{
				if ( obj.IsPrefabInstanceRoot && obj.PrefabInstanceSource == changedPath )
				{
					obj.UpdateFromPrefab();
				}
			}
		}
	}

	/// <summary>
	/// Should get called whenever the prefab scene has finished being edited
	/// </summary>
	public static void UpdatePrefabInstances( PrefabScene scene, PrefabFile prefab )
	{
		ArgumentNullException.ThrowIfNull( prefab );

		// write from prefab scene to its jsonobject
		// this doesn't save it to disk
		prefab.RootObject = scene.Serialize();

		foreach ( var session in SceneEditorSession.All )
		{
			UpdatePrefabsInScene( session.Scene, prefab );
		}

		if ( GameManager.ActiveScene is not null )
		{
			UpdatePrefabsInScene( GameManager.ActiveScene, prefab );
		}
	}

}
