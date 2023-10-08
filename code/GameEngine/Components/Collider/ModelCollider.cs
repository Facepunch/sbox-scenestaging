using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Buffers;
using System.Numerics;

[Title( "Model Collider" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ModelCollider : ColliderBaseComponent
{
	[Property] public Surface Surface { get; set; }
	[Property] public Model Model { get; set; }

	public override void DrawGizmos()
	{
		//if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
	//		return;

		if ( Model is null ) return;

		if ( Model.Physics is null ) return;

		Gizmo.Draw.Color = Gizmo.Colors.Green;

		foreach ( var part in Model.Physics.Parts )
		{
			using ( Gizmo.Scope( $"part {part.GetHashCode()}", part.Transform ) )
			{
				foreach ( var capsule in part.Capsules )
				{
					Gizmo.Draw.LineCapsule( capsule.Capsule );
				}
			}
		}

		// TODO - draw physics models from Model
	}

	protected override PhysicsShape CreatePhysicsShape( PhysicsBody targetBody )
	{
		return null;
	}

	public override void OnEnabled()
	{
		var tx = GameObject.WorldTransform;
		group = Scene.PhysicsWorld.SetupPhysicsFromModel( Model, tx, PhysicsMotionType.Keyframed );
	}
}
