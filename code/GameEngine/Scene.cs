using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Scene : GameObject
{
	public bool IsEditor { get; set; }

	public SceneWorld SceneWorld { get; private set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; private set; }
	public NavigationMesh NavigationMesh { get; set; }

	public SceneFile SourceSceneFile { get; private set; }
	public PrefabFile SourcePrefabFile { get; private set; }

	public bool HasUnsavedChanges { get; private set; }

	Gizmo.Instance gizmoInstance = new();

	public Scene() : base( true, "Scene", null  )
	{
		_scene = this;
		SceneWorld = new SceneWorld();
		PhysicsWorld = new PhysicsWorld();

		// todo - load from package
		var settings = new Sandbox.Physics.CollisionRules();
		PhysicsWorld.SetCollisionRules( settings );
	}

	public void Register( GameObject o )
	{
		o.Parent = this;

		SceneUtility.ActivateGameObject( o );
	}

	public void EditorTick()
	{
		ProcessDeletes();
		PreRender();
		DrawGizmos();
	}

	public void GameTick()
	{
		if ( IsEditor ) return; // never in editor

		gizmoInstance.Input.Camera = Sandbox.Camera.Main;

		using ( gizmoInstance.Push() )
		{
			ProcessDeletes();

			if ( GameManager.IsPaused )
				return;

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

	internal void OnParentChanged( GameObject gameObject, GameObject oldParent, GameObject parent )
	{
		if ( oldParent == null )
		{
			//All.Remove( gameObject );
		}

		if ( parent == null )
		{
			//All.Add( gameObject );
		}
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

	internal void ProcessDeletes()
	{
		if ( deleteList.Count == 0 )
			return;

		foreach ( var o in deleteList.ToArray() )
		{
			o.DestroyImmediate();
			deleteList.Remove( o );
		}
	}

	public void Load( SceneFile resource )
	{
		Assert.NotNull( resource );

		SourceSceneFile = resource;

		using var sceneScope = Push();

		using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

		if ( resource.GameObjects is not null )
		{
			foreach ( var json in resource.GameObjects )
			{
				var go = CreateObject( false );
				go.Deserialize( json );
			}
		}
	}

	public void Load( PrefabFile resource )
	{
		SourcePrefabFile = resource;

		using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

		if ( resource.RootObject is not null )
		{
			var go = CreateObject( false );
			go.Deserialize( resource.RootObject );
		}
	}


	public SceneFile Save()
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
			found.AddRange( go.GetComponents<T>( includeDisabled, true ) );
		}

		return found;
	}

	internal void Remove( GameObject gameObject )
	{
		if ( !Children.Remove( gameObject ) )
		{
			Log.Warning( "Scene.Remove - gameobject wasn't in All!" );
		}
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

	/// <summary>
	/// This is slow, and somewhat innacurate. Don't call it every frame!
	/// </summary>
	public BBox GetBounds()
	{
		var renderers = Children.SelectMany( x => x.GetComponents<ModelComponent>() );

		return BBox.FromBoxes( renderers.Select( x => x.Bounds ) );
	}

	public override void EditLog( string name, object source, Action undo )
	{
		if ( !IsEditor ) return;

		HasUnsavedChanges = true;
		//Log.Info( $"Scene Change: {name} / {source}" );
	}

	public void ClearUnsavedChanges()
	{
		if ( !IsEditor ) return;
		HasUnsavedChanges = false;
	}
}
