using Sandbox;
using System.Threading;

public sealed class TraceDebugVis : BaseComponent
{
	public enum TraceType
	{
		Ray,
		Box,
		Sphere
	}

	[Property] public TraceType Type { get; set; }
	[Property] public Vector3 BoxSize { get; set; } = 3;
	[Property] public float SphereRadius { get; set; } = 2;
	[Property] public float TraceLength { get; set; } = 100;



	public override void DrawGizmos()
	{
		var bb = new BBox( -BoxSize, BoxSize );

		Gizmo.Transform = global::Transform.Zero;

		var pos = Transform.Position;
		var rot = Transform.Rotation;

		var tr = Scene.PhysicsWorld.Trace.Ray( new Ray( pos, rot.Forward ), TraceLength );

		if ( Type == TraceType.Ray )
		{
			
		}

		if ( Type == TraceType.Box )
		{
			tr = tr.Size( bb );
		}

		if ( Type == TraceType.Sphere )
		{
			tr = tr.Radius( SphereRadius );
		}

		var r = tr.Run();

		if ( r.Body is not null )
		{
			Gizmo.Draw.Color = Color.Cyan;
			var closest = r.Body.FindClosestPoint( r.EndPosition );
			Gizmo.Draw.Line( closest, r.EndPosition );

			Gizmo.Draw.Text( $"{r.Shape}", new Transform( closest + Vector3.Down * 1 ) );
		}

		if ( r.Shape is not null )
		{
			
		}

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( r.StartPosition, r.EndPosition );
		

		if ( Type == TraceType.Box )
		{
			Gizmo.Draw.LineBBox( bb + r.StartPosition );
			Gizmo.Draw.LineBBox( bb + r.EndPosition );
		}

		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.Line( r.EndPosition, r.EndPosition + r.Normal * 2.0f );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Text( $"Normal: {r.Normal}\nFraction: {r.Fraction}", new Transform( r.EndPosition + Vector3.Down * 1 ) );

	}
}
