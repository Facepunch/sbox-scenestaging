using Sandbox;
using Sandbox.Diagnostics;

[Title( "Model Collider" )]
[Category( "Physics" )]
[Icon( "check_box_outline_blank", "red", "white" )]
public class ModelCollider : ColliderBaseComponent
{
	[Property] public Surface Surface { get; set; }
	[Property] public Model Model { get; set; }

	public override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		// TODO - draw physics models from Model
	}

	protected override PhysicsShape CreatePhysicsShape( PhysicsBody targetBody )
	{
		return null;
	}

	public override void OnEnabled()
	{
		var tx = GameObject.WorldTransform;
		group = Scene.PhysicsWorld.SetupPhysicsFromModel( Model, tx, PhysicsMotionType.Dynamic );
	}
}
