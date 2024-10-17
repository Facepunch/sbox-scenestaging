public sealed partial class PhysicalCharacterController : Component
{
	/// <summary>
	/// True if we're on a ladder
	/// </summary>
	public bool IsClimbing => ClimbingObject is not null;

	/// <summary>
	/// The GameObject we're climbing. This will usually be a ladder trigger.
	/// </summary>
	public GameObject ClimbingObject { get; set; }

	/// <summary>
	/// When climbing, this is the rotation of the wall/ladder you're climbing, where
	/// Forward is the direction to look at the ladder, and Up is the direction to climb.
	/// </summary>
	public Rotation ClimbingRotation { get; set; }


	void UpdatePositionOnLadder()
	{
		if ( !IsClimbing ) return;
		if ( !ClimbingObject.IsValid() ) return;

		var pos = WorldPosition;

		// work out ideal position
		var ladderPos = ClimbingObject.WorldPosition;
		var ladderUp = ClimbingObject.WorldRotation.Up;

		Line ladderLine = new Line( ladderPos - ladderUp * 1000, ladderPos + ladderUp * 1000 );

		var idealPos = ladderLine.ClosestPoint( pos );

		// Get just the left/right
		var delta = (idealPos - pos);
		delta = delta.SubtractDirection( ClimbingObject.WorldRotation.Forward );

		if ( delta.Length > 0.01f )
		{
			Body.Velocity = Body.Velocity.AddClamped( delta * 5.0f, delta.Length * 10.0f );
		}

	}

	/// <summary>
	/// Start climbing a ladder (or wall or something)
	/// </summary>
	private void TryStartClimbing( GameObject ladderObject )
	{
		if ( ladderObject == ClimbingObject )
			return;

		ClimbingObject = ladderObject;

		if ( ClimbingObject is not null )
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
