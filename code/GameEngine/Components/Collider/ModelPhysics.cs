using Sandbox;
using System.Collections.Generic;
using System.Linq;

[Title( "Model Physics" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ModelPhysics : Component
{
	[Property] public Model Model { get; set; }

	[Property] public SkinnedModelRenderer Renderer { get; set; }

	PhysicsGroup PhysicsGroup;

	protected override void DrawGizmos()
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

	protected override void OnEnabled()
	{
		if ( Model is null )
			return;

		PhysicsGroup = Scene.PhysicsWorld.SetupPhysicsFromModel( Model, Transform.World, PhysicsMotionType.Dynamic );
		
		foreach ( var body in PhysicsGroup.Bodies )
		{
			body.GameObject = GameObject;
		}
	}

	protected override void OnDisabled()
	{
		PhysicsGroup?.Remove();
		PhysicsGroup = null;

		if ( Renderer is not null )
		{
			Renderer.ClearPhysicsBones();
		}
	}

	protected override void OnUpdate()
	{
		if ( PhysicsGroup is null ) return;
		if ( Renderer is null ) return;

		foreach ( var body in PhysicsGroup.Bodies )
		{
			var bone = Renderer.Model.Bones.AllBones.FirstOrDefault( x => x.Name == body.GroupName );
			if ( bone is null ) continue;

			var tx = body.GetLerpedTransform( Time.Now );

			Renderer.SetBoneTransform( bone, tx );

			if ( bone.Index == 0 )
			{
				Renderer.GameObject.Transform.Position = tx.Position;
			}
		}
	}

}
