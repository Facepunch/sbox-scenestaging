using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public class Scene : GameObject
{
	public bool IsEditor { get; private set; }
	public Action<string> OnEdited { get; set; }

	public SceneWorld SceneWorld { get; private set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; private set; }
	public bool HasUnsavedChanges { get; private set; }

	public GameResource Source { get; protected set; }

	Gizmo.Instance gizmoInstance = new();

	public Scene() : base( true, "Scene", null )
	{
		_scene = this;
		SceneWorld = new SceneWorld();
		PhysicsWorld = new PhysicsWorld();

		// todo - load from package
		var settings = new Sandbox.Physics.CollisionRules();
		PhysicsWorld.SetCollisionRules( settings );
	}

	protected Scene( bool isEditor ) : this()
	{
		IsEditor = isEditor;
	}

	public static Scene CreateEditorScene()
	{
		return new Scene( true );
	}

	/// <summary>
	/// The update loop will turn certain settings on
	/// Here we turn them to their defaults.
	/// </summary>
	void InitialSettings()
	{
		SceneWorld.GradientFog.Enabled = false;
	}

	public void EditorTick()
	{
		ProcessDeletes();
		PreRender();
		DrawGizmos();
		InitialSettings();

		// Only tick here if we're an editor scene
		// The game will tick a game scene!
		if ( IsEditor )
		{
			Tick();
		}

		ProcessDeletes();
	}

	public void GameTick()
	{
		gizmoInstance.Input.Camera = Sandbox.Camera.Main;

		using ( gizmoInstance.Push() )
		{
			ProcessDeletes();

			if ( GameManager.IsPaused )
				return;

			InitialSettings();

			Tick();

			ProcessDeletes();

			TickPhysics();

			ProcessDeletes();
		}
	}


	void TickPhysics()
	{
		PhysicsWorld.Gravity = Vector3.Down * 900;
		PhysicsWorld?.Step( Time.Delta );
		PostPhysics();
	}

	public GameObject CreateObject( bool enabled = true )
	{
		using ( Push() )
		{
			var go = GameObject.Create( enabled );
			go.Enabled = enabled;
			go.Parent = this;
			return go;
		}
	}

	HashSet<GameObject> deleteList = new();

	internal void QueueDelete( GameObject gameObject )
	{
		deleteList.Add( gameObject );
	}

	public void ProcessDeletes()
	{
		if ( deleteList.Count == 0 )
			return;

		foreach ( var o in deleteList.ToArray() )
		{
			o.DestroyImmediate();
			deleteList.Remove( o );
		}
	}

	protected void Clear()
	{
		foreach ( var go in Children.ToArray() )
		{
			go.DestroyImmediate();
		}

		ForEachComponent( "Clear", false, c =>
		{
			c.Destroy();
		} );

		Components.Clear();
		Children.Clear();
	}

	public virtual void Load( GameResource resource )
	{
		Assert.NotNull( resource );

		Clear();
		ProcessDeletes();

		if ( resource is SceneFile sceneFile )
		{
			Source = sceneFile;

			using var sceneScope = Push();

			using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

			if ( sceneFile.GameObjects is not null )
			{
				foreach ( var json in sceneFile.GameObjects )
				{
					var go = CreateObject( false );
					go.Deserialize( json );
				}
			}
		}
	}

	public virtual GameResource Save()
	{
		var a = new SceneFile();
		a.GameObjects = Children.Select( x => x.Serialize() ).ToArray();
		return a;
	}

	public IEnumerable<T> FindAllComponents<T>( bool includeDisabled = false ) where T : BaseComponent
	{
		// array rent?
		List<T> found = new List<T>();

		foreach ( var go in Children )
		{
			if ( go is null ) continue;

			found.AddRange( go.GetComponents<T>( includeDisabled, true ) );
		}

		return found;
	}

	/// <summary>
	/// Push this scene as the active scene, for a scope
	/// </summary>
	public IDisposable Push()
	{
		var old = GameManager.ActiveScene;
		GameManager.ActiveScene = this;

		return DisposeAction.Create( () =>
		{
			GameManager.ActiveScene = old;
		} );
	}

	public void LoadFromFile( string filename )
	{
		// Clean Scene

		var file = ResourceLibrary.Get<SceneFile>( filename );
		if ( file is null )
		{
			Log.Warning( $"LoadFromFile: Couldn't find {filename}" );
			return;
		}

		Load( file );
	}

	public override void EditLog( string name, object source )
	{
		HasUnsavedChanges = true;
		OnEdited?.Invoke( name );
	}

	public void ClearUnsavedChanges()
	{
		HasUnsavedChanges = false;
	}

	Dictionary<Guid, GameObject> objectsById = new ();

	public int RegisteredObjectIds => objectsById.Count;

	internal void RegisterGameObjectId( GameObject go )
	{
		if ( objectsById.TryGetValue( go.Id, out var existing ) )
		{
			Log.Warning( $"{go}: Guid {go.Id} is already taken by {existing} - changing" );
			go.ForceChangeId( Guid.NewGuid() );
		}

		objectsById[go.Id] = go;
	}

	internal void RegisterGameObjectId( GameObject go, Guid previouslyKnownAs )
	{
		if ( objectsById.TryGetValue( previouslyKnownAs, out var existing ) && existing == go )
		{
			objectsById.Remove( previouslyKnownAs );
		}

		RegisterGameObjectId( go );
	}

	internal void UnregisterGameObjectId( GameObject go )
	{
		if ( !objectsById.TryGetValue( go.Id, out var existing ) )
		{
			Log.Warning( $"Tried to unregister unregistered id {go}, {go.Id}" );
			return;
		}

		if ( existing != go )
		{
			Log.Warning( $"Tried to unregister wrong game object {go}, {go.Id} (was {existing})" );
			return;
			
		}

		objectsById.Remove( go.Id );
	}

	/// <summary>
	/// Find a GameObject in the scene by Guid. This is pretty fast.
	/// </summary>
	public GameObject FindObjectByGuid( Guid guid )
	{
		if ( objectsById.TryGetValue( guid, out var found ) )
			return found;

		return null;
	}

	internal void OnRenderOverlayInternal( SceneCamera camera )
	{
		foreach ( var c in GetComponents<BaseComponent.RenderOverlay>( true, true ) )
		{
			c.OnRenderOverlay( camera );
		}
	}
}
