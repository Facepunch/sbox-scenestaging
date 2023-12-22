using Sandbox;
using System.Collections.Generic;

public sealed class CameraPhysicsDebug : Component, Component.ExecuteInEditor
{
	[Property] public int MaxPoints { get; set; } = 10000;
	[Property] public int TracesPerFrame { get; set; } = 500;

	[Range( 0, 10 )]
	[Property] public float NormalLength { get; set; } = 2;
	[Property] public TraceTypes TraceType { get; set; } = TraceTypes.Ray;
	[Property] public bool Hitboxes { get; set; } = false;

	public record struct Hitpoint( Vector3 Position, Vector3 Normal, Color Tint );

	public enum TraceTypes
	{
		Ray,
		Box,
		Sphere
	}


	List<Hitpoint> worldPoints = new();

	protected override void OnUpdate()
	{
		var start = Transform.Position;

		Gizmo.Draw.LineThickness = 2;
		Gizmo.Draw.LineSphere( start, 2.0f );

		Sandbox.Utility.Parallel.ForEach( Enumerable.Range( 0, TracesPerFrame ), i =>
		{
			SceneTraceResult t = default;
			var end = start + Transform.Rotation.Forward * 1000 + Vector3.Random * 400;

			if ( TraceType == TraceTypes.Ray )
			{
				t = Scene.Trace
						.Ray( start, end )
						.UseHitboxes( Hitboxes )
						.Run();
			}
			else if ( TraceType == TraceTypes.Box )
			{
				t = Scene.Trace
						.Ray( start, end )
						.Size( new BBox( Vector3.One * -5, Vector3.One * 5 ) )
						.UseHitboxes( Hitboxes )
						.Run();


			}
			else if ( TraceType == TraceTypes.Sphere )
			{
				t = Scene.Trace
						.Ray( start, end )
						.Radius( 5 )
						.UseHitboxes( Hitboxes )
						.Run();
			}

			if ( t.Hit )
			{
				lock ( worldPoints )
				{
					Color tint = Color.White;
					worldPoints.Add( new Hitpoint { Position = t.EndPosition, Normal = t.Normal, Tint = tint } );
				}
			}


		} );

		foreach ( var t in worldPoints )
		{
			Gizmo.Draw.Color = t.Tint * new Color( (t.Normal.x + 1) * 0.5f, (t.Normal.y + 1) * 0.5f, (t.Normal.z + 1) * 0.5f );
			Gizmo.Draw.Line( t.Position, t.Position + t.Normal * NormalLength );
		}

		if ( worldPoints.Count > MaxPoints )
		{
			worldPoints.RemoveRange( 0, worldPoints.Count - MaxPoints );
		}
	}
}
