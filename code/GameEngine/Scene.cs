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

		NavigationMesh?.DrawGizmos();
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

}
