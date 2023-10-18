using Sandbox;
using System.Collections.Generic;

[Title( "Model Physics" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ModelPhysics : BaseComponent
{
	[Property] public Model Model { get; set; }

	PhysicsGroup PhysicsGroup;

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
	
	public override void OnEnabled()
	{
		if ( Model is null )
			return;

		PhysicsGroup = Scene.PhysicsWorld.SetupPhysicsFromModel( Model, Transform.World, PhysicsMotionType.Dynamic );
	}

	public override void OnDisabled()
	{
		PhysicsGroup?.Remove();
		PhysicsGroup = null;
	}

}
