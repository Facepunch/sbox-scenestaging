using Microsoft.VisualBasic;
using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class Scene
{
	public static Scene Active { get; set; }
	public bool IsEditor { get; set; }
	public string Name { get; set; } = "New Scene";
	public SceneWorld SceneWorld { get; private set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; private set; }
	public NavigationMesh NavigationMesh { get; set; }
	public SceneSource Source { get; private set; }

	public HashSet<GameObject> All = new HashSet<GameObject>();

	Gizmo.Instance gizmoInstance = new Gizmo.Instance();

	public Scene()
	{
		SceneWorld = new SceneWorld();
		PhysicsWorld = new PhysicsWorld();
	}

	public void Register( GameObject o )
	{
		o.Scene = this;
		All.Add( o );

		o.OnCreate();
	}

	public void Unregister( GameObject o )
	{
		o.OnDestroy();
		o.Scene = null;
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

		foreach ( var e in All )
		{
			e.Tick();
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
		if ( NavigationMesh is null )
			return;

		foreach ( var area in NavigationMesh.areas.Values )
		{
			Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.2f );
			Gizmo.Draw.LineThickness = 3;

			foreach ( var triangle in area.Triangles )
			{
				Gizmo.Draw.SolidTriangle( triangle );
			}
		}

		var p = new NavigationPath( NavigationMesh );
		p.StartPoint = All.Reverse().Skip( 1 ).FirstOrDefault()?.Transform.Position ?? Camera.Main.Position + Camera.Main.Rotation.Forward * 500;
		p.EndPoint = All.Reverse().FirstOrDefault()?.Transform.Position ?? Camera.Main.Position + Camera.Main.Rotation.Forward * 500;
		p.Build();

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.ScreenText( $"Path Builder: {p.GenerationMilliseconds:0.00}ms", 100 );

		Gizmo.Draw.LineThickness = 3;

		for ( int i = 0; i < p.Segments.Count - 1; i++ )
		{
			Gizmo.Draw.Color = Color.Cyan;
			Gizmo.Draw.Line( p.Segments[i].Position, p.Segments[i + 1].Position );
			Gizmo.Draw.LineSphere( new Sphere( p.Segments[i].Position, 1 ) );

			//Gizmo.Draw.Color = Color.White;
			//Gizmo.Draw.ScreenText( $"{p.Segments[i].Distance:n0}", Camera.Main.ToScreen( p.Segments[i].Position + Vector3.Up * 10 ) );
		}
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
		var go = new GameObject() { Enabled = enabled };
		Register( go );
		return go;
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

	public void Load( SceneSource resource )
	{
		Source = resource;

		if ( resource.GameObjects is not null )
		{
			foreach( var json in resource.GameObjects )
			{
				var go = CreateObject();
				go.Deserialize( json );
			}
		}
	}

	public SceneSource Save()
	{
		var a = new SceneSource();
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
}
