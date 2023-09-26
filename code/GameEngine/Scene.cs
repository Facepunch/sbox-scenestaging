using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

[GameResource( "Scene", "scene", "A scene", Icon = "perm_media" )]
public class Scene
{
	public static Scene Active { get; set; }
	public bool IsEditor { get; set; }
	public string Name { get; set; } = "New Scene";
	public SceneWorld SceneWorld { get; private set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; private set; }
	public NavigationMesh NavigationMesh { get; set; }

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
		{
			// Tell it to use the main camera
			gizmoInstance.Input.Camera = Sandbox.Camera.Main;
		}

		using var giz = gizmoInstance.Push();

		TickObjects();
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

	public GameObject CreateObject()
	{
		var go = new GameObject();
		Register( go );
		return go;
	}
}
