using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Scene : GameObject
{
	public bool IsEditor { get; private set; }
	public Action<string> OnEdited { get; set; }

	public SceneWorld SceneWorld { get; private set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; private set; }
	public bool HasUnsavedChanges { get; private set; }

	public GameResource Source { get; protected set; }

	public GameObjectDirectory Directory { get; private set; }

	Gizmo.Instance gizmoInstance = new();

	public Scene() : base( true, "Scene" )
	{
		SceneWorld = new SceneWorld();
		PhysicsWorld = new PhysicsWorld();
		Directory = new GameObjectDirectory( this );

		PhysicsWorld.DebugSceneWorld = DebugSceneWorld;

		PhysicsWorld.Gravity = Vector3.Down * 850;
		PhysicsWorld.SimulationMode = PhysicsSimulationMode.Continuous;

		UpdateFromPackage( Game.Menu?.Package );
		Event.Register( this );
	}

	~Scene()
	{
		Event.Unregister( this );
	}

	/// <summary>
	/// Updates information like physics collision rules from a game package.
	/// </summary>
	/// <param name="package"></param>
	protected void UpdateFromPackage( Package package )
	{
		if ( package == null ) return;

		// Grab collision rules from current game package
		var settings = package.GetMeta( "Collision", new Sandbox.Physics.CollisionRules() );
		PhysicsWorld.SetCollisionRules( settings );
	}

	[Event( "addon.config.updated" )]
	protected void OnAddonConfigUpdated( Package package )
	{
		if ( Game.Menu.Package.FullIdent != package.FullIdent )
			return;

		UpdateFromPackage( package );
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
	/// Create a GameObject on this scene. This doesn't require the scene to be the active scene.
	/// </summary>
	public GameObject CreateObject( bool enabled = true )
	{
		using ( Push() )
		{
			var go = new GameObject( enabled );
			go.Enabled = enabled;
			go.Parent = this;
			return go;
		}
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


	public override void EditLog( string name, object source )
	{
		HasUnsavedChanges = true;
		OnEdited?.Invoke( name );
	}

	public void ClearUnsavedChanges()
	{
		HasUnsavedChanges = false;
	}


	internal void OnRenderOverlayInternal( SceneCamera camera )
	{
		foreach ( var c in GetComponents<BaseComponent.RenderOverlay>( true, true ) )
		{
			c.OnRenderOverlay( camera );
		}
	}
}
