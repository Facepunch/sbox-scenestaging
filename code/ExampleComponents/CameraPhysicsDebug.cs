using Sandbox;
using System.Collections.Generic;

public sealed class CameraPhysicsDebug : BaseComponent
{
	[Property] public int MaxPoints { get; set; } = 10000;
	[Property] public int TracesPerFrame { get; set; } = 500;

	[Range( 0, 10 )]
	[Property] public float NormalLength { get; set; } = 2;
	[Property] public TraceTypes TraceType { get; set; } = TraceTypes.Ray;

	public record struct Hitpoint( Vector3 Position, Vector3 Normal );

	public enum TraceTypes
	{
		Ray,
		Box,
		Sphere
	}


	List<Hitpoint> worldPoints = new();

	public override void Update()
	{
		for ( int i = 0; i < TracesPerFrame; i++ )
		{
			PhysicsTraceResult t = default;
			var start = Transform.Position;
			var end = Transform.Position + Transform.Rotation.Forward * 1000 + Vector3.Random * 400;

			if ( TraceType == TraceTypes.Ray )
			{
				t = Physics.Trace
						.Ray( start, end )
						.Run();
			}
			else if (  TraceType == TraceTypes.Box )
			{
				t = Physics.Trace
						.Ray( start, end )
						.Size( new BBox( -10, 10 ) )
						.Run();
			}
			else if ( TraceType == TraceTypes.Sphere )
			{
				t = Physics.Trace
						.Ray( start, end )
						.Radius( 20 )
						.Run();
			}

			if ( t.Hit )
			{
				worldPoints.Add( new Hitpoint { Position = t.HitPosition, Normal = t.Normal } );
			}


		}

		foreach ( var t in worldPoints )
		{
			Gizmo.Draw.Color = new Color( (t.Normal.x + 1) * 0.5f, (t.Normal.y + 1) * 0.5f, (t.Normal.z + 1) * 0.5f );
			Gizmo.Draw.Line( t.Position, t.Position + t.Normal * NormalLength );
		}

		if ( worldPoints.Count > MaxPoints )
		{
			worldPoints.RemoveRange( 0, worldPoints.Count - MaxPoints );
		}
	}
}
