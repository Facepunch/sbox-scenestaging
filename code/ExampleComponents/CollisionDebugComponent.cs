using Sandbox;
using Sandbox.Services;
using System;

public sealed class CollisionDebugComponent : Component
{
	[Property] GameObject DebugPoint { get; set; }

	protected override void OnEnabled()
	{
		//Scene.PhysicsWorld.Internal_OnCollision += PhysicsCollision;
	}

	protected override void OnDisabled()
	{
		//Scene.PhysicsWorld.Internal_OnCollision -= PhysicsCollision;
	}

	private void PhysicsCollision( int arg1, PhysicsBody body1, PhysicsBody body2, Vector3 vector )
	{
		if ( vector == default )
			return;

		SceneUtility.Instantiate( DebugPoint, vector, Rotation.Identity );
	}
}
