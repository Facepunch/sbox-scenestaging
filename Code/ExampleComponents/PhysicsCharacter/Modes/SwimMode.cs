namespace Sandbox.PhysicsCharacterMode;

/// <summary>
/// The character is walking
/// </summary>
[Icon( "🏊" ), Group( "PhysicsCharacterMode" ), Title( "Swim Mode" )]
public partial class PhysicsCharacterSwimMode : BaseMode
{
	public override void UpdateRigidBody( Rigidbody body )
	{
		body.Gravity = false;
		body.LinearDamping = 3.3f;
		body.AngularDamping = 1f;
	}

	public override int Score( PhysicsCharacter controller )
	{
		if ( controller.WaterLevel > 0.7f ) return 10;

		return -10;
	}

	public override void OnModeEnd( BaseMode next )
	{
		// jump when leaving the water
		if ( Input.Down( "Jump" ) )
		{
			Controller.Jump( Vector3.Up * 300 );
		}
	}
}
