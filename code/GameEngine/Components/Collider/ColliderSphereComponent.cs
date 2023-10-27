using Sandbox;
using Sandbox.Diagnostics;
using System.Collections.Generic;

[Title( "Collider - Sphere" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
[Alias( "SphereColliderComponent" )]
public class ColliderSphereComponent : Collider
{
	float _radius = 10.0f;

	[Property]
	public float Radius
	{
		get => _radius;
		set
		{
			if ( _radius == value ) return;

			_radius = value;
			Rebuild();
		}
	}

	public override void DrawGizmos()
	{
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsChildSelected ? 0.5f : 0.1f );
		Gizmo.Draw.LineSphere( new Sphere( 0, Radius ) );
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		var tx = targetBody.Transform.ToLocal( Transform.World );
		var shape = targetBody.AddSphereShape( tx.Position, Radius * tx.Scale );

		if ( Surface is not null )
		{
			shape.SurfaceMaterial = Surface.ResourceName;
		}

		yield return shape;
	}
}
