using Sandbox;
using System.Threading;

public sealed class TraceDebugVis : Component
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
	[Property] public List<GameObject> IgnoreSingleObjects { get; set; }
	[Property] public List<GameObject> IgnoreHierarchy { get; set; }
	[Property] public TagSet IgnoreTags { get; set; }
	[Property] public bool IncludeHitboxes { get; set; } = true;

	protected override void OnUpdate()
	{
		DrawGizmos();
	}

	protected override void DrawGizmos()
	{
		var bb = new BBox( -BoxSize, BoxSize );

		Gizmo.Transform = global::Transform.Zero;

		var pos = Transform.Position;
		var rot = Transform.Rotation;

		var tr = Scene.Trace.Ray( new Ray( pos, rot.Forward ), TraceLength );

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

		if ( IgnoreSingleObjects is not null )
		{
			foreach( var obj in IgnoreSingleObjects )
			{
				tr = tr.IgnoreGameObject( obj );
			}
		}

		if ( IgnoreHierarchy is not null )
		{
			foreach ( var obj in IgnoreHierarchy )
			{
				tr = tr.IgnoreGameObjectHierarchy( obj );
			}
		}

		if ( IgnoreTags  is not null )
		{
			tr = tr.WithoutTags( IgnoreTags );
		}

		if ( IncludeHitboxes )
		{
			tr = tr.UseHitboxes();
		}

		var r = tr.Run();

		if ( r.GameObject is not null )
		{
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.Text( $"{r.GameObject?.Name}", new Transform( r.HitPosition + Vector3.Down * 7 ), "Poppins", 18 );
		}

		if ( r.Shape is not null )
		{
			
		}

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
		Gizmo.Draw.LineThickness = 4;
		Gizmo.Draw.Line( r.StartPosition, r.EndPosition );
		

		if ( Type == TraceType.Box )
		{
			Gizmo.Draw.LineBBox( bb + r.StartPosition );
			Gizmo.Draw.LineBBox( bb + r.EndPosition );
		}

		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.Line( r.EndPosition, r.EndPosition + r.Normal * 2.0f );

		//Gizmo.Draw.Color = Color.White;
		//Gizmo.Draw.Text( $"Normal: {r.Normal}\nFraction: {r.Fraction}", new Transform( r.EndPosition + Vector3.Down * 1 ) );

	}
}
