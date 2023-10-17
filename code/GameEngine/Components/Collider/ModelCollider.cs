using Sandbox;
using System.Collections.Generic;

[Title( "Model Collider" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ModelCollider : ColliderBaseComponent
{
	[Property] public Model Model { get; set; }

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

		// TODO - draw physics models from Model
	}

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
		var tx = targetBody.Transform.ToLocal( Transform.World );

		foreach ( var part in Model.Physics.Parts )
		{
			foreach ( var sphere in part.Spheres )
			{
				yield return targetBody.AddSphereShape( tx.PointToWorld( sphere.Sphere.Center ), sphere.Sphere.Radius * tx.Scale );
			}

			foreach ( var capsule in part.Capsules )
			{
				yield return targetBody.AddCapsuleShape( tx.PointToWorld( capsule.Capsule.CenterA ), tx.PointToWorld( capsule.Capsule.CenterB ), capsule.Capsule.Radius * tx.Scale );
			}

			foreach ( var hull in part.Hulls )
			{
				yield return targetBody.AddShape( hull, tx );
			}

			foreach ( var mesh in part.Meshes )
			{
				yield return targetBody.AddShape( mesh, tx, false, true );
			}
		}
	}

	
	public override void OnEnabled()
	{
		//
		// When we create the model physics manually, it's all fucked.
		// but when we use SetupPhysicsFromModel it all works. Why?
		//
		//  - garry
		//
		bool createModelPhysicsManually = false;

		if ( createModelPhysicsManually )
		{

			base.OnEnabled();
		}
		else
		{
			group = Scene.PhysicsWorld.SetupPhysicsFromModel( Model, Transform.World, PhysicsMotionType.Keyframed );
		}
	}
	
}
