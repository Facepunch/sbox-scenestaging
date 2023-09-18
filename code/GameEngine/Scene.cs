using Sandbox;
using System.Collections.Generic;
using static Sandbox.Gizmo;

public class Scene
{
	public static Scene Active { get; set; }

	public SceneWorld SceneWorld { get; set; }
	public SceneWorld DebugSceneWorld => gizmoInstance.World;
	public PhysicsWorld PhysicsWorld { get; set; }
	public NavigationMesh NavigationMesh { get; set; }

	public HashSet<GameObject> All = new HashSet<GameObject>();

	Gizmo.Instance gizmoInstance = new Gizmo.Instance();


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

		TickPhysics();

		

		DrawNavmesh();
	}

	void TickPhysics()
	{
		PhysicsWorld.Gravity = Vector3.Down * 900;
		PhysicsWorld?.Step( Time.Delta );
		PostPhysics();
	}

	void PostPhysics()
	{
		foreach( var e in All )
		{

			e.PostPhysics();
		}
	}

	void DrawNavmesh()
	{
		foreach ( var area in NavigationMesh.areas.Values )
		{
			Gizmo.Draw.Color = Color.White;

			var c = area.Vertices.Count;
			for ( int i = 0; i < c; i++ )
			{
				Gizmo.Draw.Line( area.Vertices[i], area.Vertices[(i + 1) % c] );
			}

			Gizmo.Draw.Line( area.Center, area.Center + area.Normal * 10.0f );

			foreach( var connect in area.Connections )
			{
				if ( connect.twoWay == false )
					continue;

				var a = area.Center;
				var b = connect.Target.Center;
				var delta = b - a;
				a += delta * 0.1f;
				b += delta * -0.1f;

				Gizmo.Draw.Color = connect.twoWay ? Color.White : Color.Red;

				Gizmo.Draw.Line( a, b );
				Gizmo.Draw.LineBBox( new BBox( b, 5 ) );
				//Gizmo.Draw.SolidCone( a, delta.Normal * 10.0f, 2.0f, 4 );
			}

			
		}
	}

}
