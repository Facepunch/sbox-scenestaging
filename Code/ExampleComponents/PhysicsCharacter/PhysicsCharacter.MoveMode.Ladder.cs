public sealed partial class PhysicsCharacter : Component
{
	/// <summary>
	/// True if we're on a ladder
	/// </summary>
	public bool IsClimbing => Mode is Sandbox.PhysicsCharacterMode.PhysicsCharacterLadderMode;

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
}
