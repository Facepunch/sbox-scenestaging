public sealed partial class PhysicalCharacterController : Component
{
	public ClimbMoveMode Climb { get; } = new ClimbMoveMode();

	[Property, Group( "Move Mode" )]
	public bool EnableClimbing
	{
		get => Climb.Enabled;
		set => Climb.Enabled = value;
	}

	/// <summary>
	/// True if we're on a ladder
	/// </summary>
	public bool IsClimbing => CurrentMoveMode == Climb;

	/// <summary>
	/// The GameObject we're climbing. This will usually be a ladder trigger.
	/// </summary>
	public GameObject ClimbingObject { get; set; }

	/// <summary>
	/// When climbing, this is the rotation of the wall/ladder you're climbing, where
	/// Forward is the direction to look at the ladder, and Up is the direction to climb.
	/// </summary>
	public Rotation ClimbingRotation { get; set; }


	/// <summary>
	/// Start climbing a ladder (or wall or something)
	/// </summary>
	private void TryStartClimbing( GameObject ladderObject )
	{
		if ( ladderObject == ClimbingObject )
			return;

		ClimbingObject = ladderObject;

		if ( ClimbingObject.IsValid() )
		{
			// work out rotation to the ladder. We could be climbing up the front or back of this thing.

			var directionToLadder = ClimbingObject.WorldPosition - WorldPosition;

			ClimbingRotation = ClimbingObject.WorldRotation;

			if ( directionToLadder.Dot( ClimbingRotation.Forward ) < 0 )
			{
				ClimbingRotation *= new Angles( 0, 180, 0 );
			}
		}
	}

	public class ClimbMoveMode : MoveMode
	{
		public override void UpdateRigidBody( Rigidbody body )
		{
			body.Gravity = false;
			body.LinearDamping = 10.0f;
			body.AngularDamping = 1f;
		}

		public override int Score( PhysicalCharacterController controller )
		{
			if ( controller.ClimbingObject.IsValid() ) return 5;

			return 0;
		}

		public override void OnUpdate( PhysicalCharacterController controller )
		{
			UpdatePositionOnLadder( controller );
		}

		void UpdatePositionOnLadder( PhysicalCharacterController controller )
		{
			if ( !controller.ClimbingObject.IsValid() ) return;

			var pos = controller.WorldPosition;

			// work out ideal position
			var ladderPos = controller.ClimbingObject.WorldPosition;
			var ladderUp = controller.ClimbingObject.WorldRotation.Up;

			Line ladderLine = new Line( ladderPos - ladderUp * 1000, ladderPos + ladderUp * 1000 );

			var idealPos = ladderLine.ClosestPoint( pos );

			// Get just the left/right
			var delta = (idealPos - pos);
			delta = delta.SubtractDirection( controller.ClimbingObject.WorldRotation.Forward );

			if ( delta.Length > 0.01f )
			{
				controller.Body.Velocity = controller.Body.Velocity.AddClamped( delta * 5.0f, delta.Length * 10.0f );
			}

		}
	}
}
