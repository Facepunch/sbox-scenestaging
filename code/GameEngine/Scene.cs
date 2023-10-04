using Microsoft.VisualBasic;
using Sandbox;
using Sandbox.Physics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed class Scene
{
	public static Scene Active { get; set; }
	public bool IsEditor { get; set; }
	public string Name { get; set; } = "New Scene";
	public SceneWorld SceneWorld { get; private set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; private set; }
	public NavigationMesh NavigationMesh { get; set; }
	public SceneFile Source { get; private set; }

	public List<GameObject> All = new ();

	Gizmo.Instance gizmoInstance = new ();

	public Scene()
	{
		SceneWorld = new SceneWorld();
		PhysicsWorld = new PhysicsWorld();

		//var settings = new CollisionRules();
		//PhysicsWorld.SetCollisionRules( settings );
	}

	public void Register( GameObject o )
	{
		if ( !All.Contains( o ) )
		{
			All.Add( o );
		}

		SceneUtility.ActivateGameObject( o );
	}

	public void Unregister( GameObject o )
	{
		o.OnDestroy();
		All.Remove( o );
	}

	public void PreRender()
	{
		foreach ( var e in All )
		{
			e.PreRender();
		}
	}


	public void Tick()
	{
		if ( IsEditor )
		{
			ProcessDeletes();
			return;
		}

		{
			// Tell it to use the main camera
			gizmoInstance.Input.Camera = Sandbox.Camera.Main;
		}

		using var giz = gizmoInstance.Push();

		if ( GameManager.IsPaused )
		{
			ProcessDeletes();
			return;
		}

		TickObjects();
		ProcessDeletes();
		TickPhysics();

		DrawNavmesh();
	}

	public void TickObjects()
	{
		for ( int i=0; i < All.Count; i++ )
		{
			All[i].Tick();
		}
	}

	void TickPhysics()
	{
		PhysicsWorld.Gravity = Vector3.Down * 900;
		PhysicsWorld?.Step( Time.Delta );
		PostPhysics();
	}

	void PostPhysics()
	{
		foreach ( var e in All )
		{
			e.PostPhysics();
		}
	}

	public void DrawGizmos()
	{
		foreach( var e in All )
		{
			e.DrawGizmos();
		}
	}

	void DrawNavmesh()
	{

	}

	internal void OnParentChanged( GameObject gameObject, GameObject oldParent, GameObject parent )
	{
		if ( oldParent == null )
		{
			All.Remove( gameObject );
		}

		if ( parent == null )
		{
			All.Add( gameObject );
		}
	}

	public GameObject CreateObject( bool enabled = true )
	{
		using ( Push() )
		{
			var go = GameObject.Create( enabled );
			go.Enabled = enabled;
			Register( go );
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

		foreach( var o in deleteList.ToArray() )
		{
			o.DestroyImmediate();
			deleteList.Remove( o );
		}
	}

	public void Load( SceneFile resource )
	{
		Source = resource;

		using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

		if ( resource.GameObjects is not null )
		{
			foreach( var json in resource.GameObjects )
			{
				var go = CreateObject( false );
				go.Deserialize( json );
			}
		}
	}

	public SceneFile Save()
	{
		var a = new SceneFile();
		a.GameObjects = All.Select( x => x.Serialize() ).ToArray();
		return a;
	}

	public IEnumerable<T> FindAllComponents<T>( bool includeDisabled = false ) where T : GameObjectComponent
	{
		// array rent?
		List<T> found = new List<T>();

		foreach( var go in All )
		{
			found.AddRange( go.GetComponents<T>( includeDisabled, true ) );
		}

		return found;
	}

	internal void Remove( GameObject gameObject )
	{
		if ( !All.Remove( gameObject ) )
		{
			Log.Warning( "Scene.Remove - gameobject wasn't in All!" );
		}
	}

	/// <summary>
	/// Find a GameObject by Guid
	/// </summary>
	public GameObject FindObjectByGuid( Guid guid )
	{
		var o =  All.Select( x => x.FindObjectByGuid( guid ) ).Where( x => x is not null ).FirstOrDefault();

		if ( o is null )
		{
			Log.Warning( $"Couldn't find object with guid {guid}" );
		}

		return o;
	}

	/// <summary>
	/// Push this scene as the active scene, for a scope
	/// </summary>
	public IDisposable Push()
	{
		var old = Active;
		Active = this;

		return DisposeAction.Create( () =>
		{
			Active = old;
		} );
	}
}
