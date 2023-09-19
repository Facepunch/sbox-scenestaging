using Sandbox;
using Sandbox.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using static Sandbox.Input;

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
		if ( NavigationMesh is null )
			return;

		foreach ( var area in NavigationMesh.areas.Values )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( 0.03f );

			var c = area.Vertices.Count;
			for ( int i = 0; i < c; i++ )
			{
				Gizmo.Draw.Line( area.Vertices[i], area.Vertices[(i + 1) % c] );
			}
		}

		var p = new NavigationPath( NavigationMesh );
		p.StartPoint = All.Reverse().Skip( 1 ).FirstOrDefault()?.Transform.Position ?? Camera.Main.Position + Camera.Main.Rotation.Forward * 500;
		p.EndPoint = All.Reverse().FirstOrDefault()?.Transform.Position ?? Camera.Main.Position + Camera.Main.Rotation.Forward * 500;
		p.Build();

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.ScreenText( $"Built Time: {p.GenerationMilliseconds:0.00}ms", 100 );

		Gizmo.Draw.LineThickness = 3;

		for ( int i=0; i< p.Segments.Count-1; i++ )
		{
			Gizmo.Draw.Color = Color.Green;
			Gizmo.Draw.Line( p.Segments[i].Position, p.Segments[i+1].Position );
			Gizmo.Draw.LineSphere( new Sphere( p.Segments[i].Position, 3 ) );

			//Gizmo.Draw.Color = Color.White;
			//Gizmo.Draw.ScreenText( $"{p.Segments[i].Distance:n0}", Camera.Main.ToScreen( p.Segments[i].Position + Vector3.Up * 10 ) );
		}
	}

}
