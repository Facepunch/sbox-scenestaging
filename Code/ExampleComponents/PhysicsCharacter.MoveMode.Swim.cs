public sealed partial class PhysicsCharacter : Component
{
	public SwimMoveMode Swimming { get; } = new SwimMoveMode();

	[Property, Group( "Move Mode" )]
	public bool EnableSwimming
	{
		get => Swimming.Enabled;
		set => Swimming.Enabled = value;
	}

	/// <summary>
	/// Will will update this based on how much you're in a "water" tagged trigger
	/// </summary>
	public float WaterLevel { get; private set; }

	/// <summary>
	/// This is for you to change
	/// </summary>
	public bool IsSwimming => CurrentMoveMode == Swimming;


	public class SwimMoveMode : MoveMode
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

		public override void OnDisabled( PhysicsCharacter controller )
		{
			// jump when leaving the water
			if ( Input.Down( "Jump" ) )
			{
				controller.Jump( Vector3.Up * 300 );
			}
		}
	}
}
