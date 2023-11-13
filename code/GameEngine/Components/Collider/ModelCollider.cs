using Sandbox;
using System.Collections.Generic;

[Title( "Model Collider" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ModelCollider : Collider
{
	Model _model;

	[Property]
	public Model Model
{
		get => _model;
		set
		{
			if ( _model == value ) return;

			_model = value;
			Rebuild();
		}
	}

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		if ( Model is null ) return;

		if ( Model.Physics is null ) return;

		Gizmo.Draw.Color = Gizmo.Colors.Green;

		foreach ( var part in Model.Physics.Parts )
		{
			using ( Gizmo.Scope( $"part {part.GetHashCode()}", part.Transform ) )
			{
				foreach ( var sphere in part.Spheres )
				{
					Gizmo.Draw.LineSphere( sphere.Sphere );
				}

				foreach ( var capsule in part.Capsules )
				{
					Gizmo.Draw.LineCapsule( capsule.Capsule );
				}

				foreach ( var hull in part.Hulls )
				{
					Gizmo.Draw.Lines( hull.GetLines() );
				}

				foreach ( var mesh in part.Meshes )
				{
					Gizmo.Draw.LineTriangles( mesh.GetTriangles() );
				}
			}
		}
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		if ( Model is null )
			yield break;

		var bodyTransform = targetBody.Transform.ToLocal( Transform.World );

		foreach ( var part in Model.Physics.Parts )
		{
			// Bone transform
			var bx = bodyTransform.ToWorld( part.Transform );

			foreach ( var sphere in part.Spheres )
			{
				var shape = targetBody.AddSphereShape( bx.PointToWorld( sphere.Sphere.Center ), sphere.Sphere.Radius * bx.Scale );
				shape.Surface = sphere.Surface;
				yield return shape;
			}

			foreach ( var capsule in part.Capsules )
			{
				var shape = targetBody.AddCapsuleShape( bx.PointToWorld( capsule.Capsule.CenterA ), bx.PointToWorld( capsule.Capsule.CenterB ), capsule.Capsule.Radius * bodyTransform.Scale );
				shape.Surface = capsule.Surface;
				yield return shape;
			}

			foreach ( var hull in part.Hulls )
			{
				var shape = targetBody.AddShape( hull, bx );
				shape.Surface = hull.Surface;
				yield return shape;
			}

			foreach ( var mesh in part.Meshes )
			{
				var shape = targetBody.AddShape( mesh, bx, false, true );
				shape.Surface = mesh.Surface;
				yield return shape;
			}
		}
	}
	
}
