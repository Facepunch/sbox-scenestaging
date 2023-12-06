using Sandbox;
using Sandbox.Physics;

[Title( "Fixed Joint" )]
[Category( "Physics" )]
[Icon( "join_inner", "red", "white" )]
[EditorHandle( "materials/gizmo/pinned.png" )]
public sealed class FixedJoint : Joint
{
	private Sandbox.Physics.FixedJoint fixedJoint;

	protected override PhysicsJoint CreateJoint( PhysicsBody body1, PhysicsBody body2 )
	{
		fixedJoint = PhysicsJoint.CreateFixed( body1, body2 );
		return fixedJoint;
	}
}
