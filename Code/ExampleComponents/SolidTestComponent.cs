using System;
using Sandbox.Solids;
using Vertex = Sandbox.Solids.Vertex;

public sealed class SolidTestComponent : Component
{
	public Solid? Solid { get; set; }

	[Property]
	public Vector3 Size { get; set; } = 256f;

	[Property]
	public int Shift { get; set; } = 8;

	[Property]
	public SolidMaterial? Material { get; set; }

	[Property] public List<GameObject> IntersectTest { get; set; } = new();

	[Button]
	public void Generate()
	{
		if ( Material is not { } material ) return;

		var min = (-Size * 0.5f).ToFixed( Shift );
		var max = (Size * 0.5f).ToFixed( Shift );

		//Solid = Solid.Tetrahedron(
		//	new Vertex( min.X, min.Y, min.Z ),
		//	new Vertex( max.X, max.Y, min.Z ),
		//	new Vertex( min.X, max.Y, max.Z ),
		//	new Vertex( max.X, min.Y, max.Z ),
		//	material );

		Solid = Solid.Box( min, max, material );
	}

	protected override void DrawGizmos()
	{
		if ( Solid is { } solid )
		{
			if ( IntersectTest is [{ WorldPosition: var posA }, { WorldPosition: var posB }, { WorldPosition: var posC }] )
			{
				var vertA = posA.ToFixed( Shift );
				var vertB = posB.ToFixed( Shift );
				var vertC = posC.ToFixed( Shift );

				Gizmo.Draw.Color = Color.Green;

				Gizmo.Draw.Line( vertA.FromFixed( Shift ), vertB.FromFixed( Shift ) );
				Gizmo.Draw.Line( vertB.FromFixed( Shift ), vertC.FromFixed( Shift ) );
				Gizmo.Draw.Line( vertC.FromFixed( Shift ), vertA.FromFixed( Shift ) );

				solid = solid.Split( vertA, vertB, vertC );
			}

			Gizmo.Draw.Color = Color.White;
			solid.DrawGizmos( Shift );
		}
	}
}
