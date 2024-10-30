namespace Sandbox.Movement;

/// <summary>
/// The character is walking
/// </summary>
[Icon( "transfer_within_a_station" ), Group( "Movement" ), Title( "MoveMode - Walk" ), Alias( "Sandbox.PhysicsCharacterMode.PhysicsCharacterWalkMode" )]
public partial class MoveModeWalk : MoveMode
{
	[Property] public int Priority { get; set; } = 0;

	[Property] public float GroundAngle { get; set; } = 45.0f;
	[Property] public float StepUpHeight { get; set; } = 18.0f;
	[Property] public float StepDownHeight { get; set; } = 18.0f;


	public override bool AllowGrounding => true;
	public override bool AllowFalling => true;

	public override int Score( PlayerController controller ) => Priority;

	public override void AddVelocity()
	{
		Controller.WishVelocity = Controller.WishVelocity.WithZ( 0 );
		base.AddVelocity();
	}

	public override void PrePhysicsStep()
	{
		base.PrePhysicsStep();

		if ( StepUpHeight > 0 )
		{
			TrySteppingUp( StepUpHeight );
		}

	}

	public override void PostPhysicsStep()
	{
		base.PostPhysicsStep();

		if ( StepDownHeight > 0 )
		{
			StickToGround( StepDownHeight );
		}

	}

	public override bool IsStandableSurace( in SceneTraceResult result )
	{
		if ( Vector3.GetAngle( Vector3.Up, result.Normal ) > GroundAngle )
			return false;

		return true;
	}

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
		// ignore pitch when walking
		eyes = eyes.Angles() with { pitch = 0 };

		return base.UpdateMove( eyes, input );
	}
}
