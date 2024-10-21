namespace Sandbox.PhysicsCharacterMode;

/// <summary>
/// The character is climbing up a ladder
/// </summary>
public partial class PhysicsCharacterLadderMode : BaseMode
{
	public override void UpdateRigidBody( Rigidbody body )
	{
		body.Gravity = false;
		body.LinearDamping = 10.0f;
		body.AngularDamping = 1f;
	}

	public override int Score( PhysicsCharacter controller )
	{
		if ( controller.ClimbingObject.IsValid() ) return 5;

		return 0;
	}

	public override void PostPhysicsStep()
	{
		UpdatePositionOnLadder();
	}


	void UpdatePositionOnLadder()
	{
		if ( !Controller.ClimbingObject.IsValid() ) return;

		var pos = Controller.WorldPosition;

		// work out ideal position
		var ladderPos = Controller.ClimbingObject.WorldPosition;
		var ladderUp = Controller.ClimbingObject.WorldRotation.Up;

		Line ladderLine = new Line( ladderPos - ladderUp * 1000, ladderPos + ladderUp * 1000 );

		var idealPos = ladderLine.ClosestPoint( pos );

		// Get just the left/right
		var delta = (idealPos - pos);
		delta = delta.SubtractDirection( Controller.ClimbingObject.WorldRotation.Forward );

		if ( delta.Length > 0.01f )
		{
			Controller.Body.Velocity = Controller.Body.Velocity.AddClamped( delta * 5.0f, delta.Length * 10.0f );
		}
	}
}
