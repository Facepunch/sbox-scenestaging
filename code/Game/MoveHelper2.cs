namespace Sandbox;

/// <summary>
/// This is the HL2 style movement. If moving from <see cref="Position"/> using <see cref="Velocity"/> results
/// in a collision, velocity will be changed to slide across the surface where
/// appropriate. Position will be updated to the optimal position.
///
///  This is coded to be simple on purpose. It's enough to get your started. Once you
///	 reach the point where it's lacking you should copy and paste it into your project
///	 and specialize to your needs.
///
/// Give it a position and velocity, set the Trace up how you want to
/// use it, then you're good to go.
///
/// </summary>
public struct MoveHelper2
{
	//
	// Inputs and Outputs
	//
	public Vector3 Position;
	public Vector3 Velocity;
	public bool HitWall;

	//
	// Config
	//
	public float GroundBounce;
	public float WallBounce;
	public float MaxStandableAngle;
	public PhysicsTraceBuilder Trace;

	/// <summary>
	/// Create the movehelper and initialize it with the default settings.
	/// You can change Trace and MaxStandableAngle after creation.
	/// </summary>
	/// <example>
	/// var move = new MoveHelper( Position, Velocity )
	/// </example>
	public MoveHelper2( PhysicsWorld world, Vector3 position, Vector3 velocity, params string[] solidTags ) : this()
	{
		Velocity = velocity;
		Position = position;
		GroundBounce = 0.0f;
		WallBounce = 0.0f;
		MaxStandableAngle = 10.0f;

		Trace = world.Trace.Ray( 0, 0 ).WithAnyTags( solidTags );
	}

	/// <summary>
	/// Create the movehelper and initialize it with the default settings.
	/// You can change Trace and MaxStandableAngle after creation.
	/// </summary>
	/// <example>
	/// var move = new MoveHelper( Position, Velocity )
	/// </example>
	public MoveHelper2( PhysicsWorld world, Vector3 position, Vector3 velocity ) : this( world, position, velocity, "solid", "playerclip", "passbullets", "player" )
	{

	}

	/// <summary>
	/// Trace this from one position to another
	/// </summary>
	public PhysicsTraceResult TraceFromTo( Vector3 start, Vector3 end )
	{
		return Trace.FromTo( start, end ).Run();
	}

	/// <summary>
	/// Trace this from its current Position to a delta
	/// </summary>
	public PhysicsTraceResult TraceDirection( Vector3 down )
	{
		return TraceFromTo( Position, Position + down );
	}


	/// <summary>
	/// Try to move to the position. Will return the fraction of the desired velocity that we traveled.
	/// Position and Velocity will be what we recommend using.
	/// </summary>
	public float TryMove( float timestep )
	{
		var timeLeft = timestep;
		float travelFraction = 0;
		HitWall = false;

		using var moveplanes = new VelocityClipPlanes( Velocity );

		for ( int bump = 0; bump < moveplanes.Max; bump++ )
		{
			if ( Velocity.Length.AlmostEqual( 0.0f ) )
				break;

			var pm = TraceFromTo( Position, Position + Velocity * timeLeft );

			travelFraction += pm.Fraction;

			if ( pm.Hit )
			{
				// There's a bug with sweeping where sometimes the end position is starting in solid, so we get stuck.
				// Push back by a small margin so this should never happen.
				Position = pm.EndPosition + pm.Normal * 0.03125f;
			}
			else
			{
				Position = pm.EndPosition;

				break;
			}

			moveplanes.StartBump( Velocity );

			if ( bump == 0 && pm.Hit && pm.Normal.Angle( Vector3.Up ) >= MaxStandableAngle )
			{
				HitWall = true;
			}

			timeLeft -= timeLeft * pm.Fraction;

			if ( !moveplanes.TryAdd( pm.Normal, ref Velocity, IsFloor( pm ) ? GroundBounce : WallBounce ) )
				break;
		}

		if ( travelFraction == 0 )
			Velocity = 0;

		return travelFraction;
	}

	/// <summary>
	/// Return true if this is the trace is a floor. Checks hit and normal angle.
	/// </summary>
	public bool IsFloor( PhysicsTraceResult tr )
	{
		if ( !tr.Hit ) return false;
		return tr.Normal.Angle( Vector3.Up ) < MaxStandableAngle;
	}

	/// <summary>
	/// Apply an amount of friction to the velocity
	/// </summary>
	public void ApplyFriction( float frictionAmount, float delta )
	{
		float StopSpeed = 100.0f;

		var speed = Velocity.Length;
		if ( speed < 0.1f ) return;

		// Bleed off some speed, but if we have less than the bleed
		//  threshold, bleed the threshold amount.
		float control = (speed < StopSpeed) ? StopSpeed : speed;

		// Add the amount to the drop amount.
		var drop = control * delta * frictionAmount;

		// scale the velocity
		float newspeed = speed - drop;
		if ( newspeed < 0 ) newspeed = 0;
		if ( newspeed == speed ) return;

		newspeed /= speed;
		Velocity *= newspeed;
	}

	/// <summary>
	/// Move our position by this delta using trace. If we hit something we'll stop,
	/// we won't slide across it nicely like TryMove does.
	/// </summary>
	public PhysicsTraceResult TraceMove( Vector3 delta )
	{
		var tr = TraceFromTo( Position, Position + delta );
		Position = tr.EndPosition;
		return tr;
	}

	/// <summary>
	/// Like TryMove but will also try to step up if it hits a wall
	/// </summary>
	public float TryMoveWithStep( float timeDelta, float stepsize )
	{
		var startPosition = Position;

		// Make a copy of us to stepMove
		var stepMove = this;

		// Do a regular move
		var fraction = TryMove( timeDelta );

		// If it got all the way then that's cool, use it
		//if ( fraction.AlmostEqual( 0 ) )
		//	return fraction;

		// Move up (as much as we can)
		stepMove.TraceMove( Vector3.Up * stepsize );

		// Move across (using existing velocity)
		var stepFraction = stepMove.TryMove( timeDelta );

		// Move back down
		var tr = stepMove.TraceMove( Vector3.Down * stepsize );

		// if we didn't land on something, return
		if ( !tr.Hit ) return fraction;

		// If we landed on a wall then this is no good
		if ( tr.Normal.Angle( Vector3.Up ) > MaxStandableAngle )
			return fraction;

		// if the original non stepped attempt moved further use that
		if ( startPosition.Distance( Position.WithZ( startPosition.z ) ) > startPosition.Distance( stepMove.Position.WithZ( startPosition.z ) ) )
			return fraction;

		// step move moved further, copy its data to us
		Position = stepMove.Position;
		Velocity = stepMove.Velocity;
		HitWall = stepMove.HitWall;

		return stepFraction;
	}

	/// <summary>
	/// Test whether we're stuck, and if we are then unstuck us
	/// </summary>
	public bool TryUnstuck()
	{
		var tr = TraceFromTo( Position, Position );
		if ( !tr.StartedSolid ) return true;

		return Unstuck();
	}

	/// <summary>
	/// We're inside something solid, lets try to get out of it.
	/// </summary>
	bool Unstuck()
	{

		//
		// Try going straight up first, people are most of the time stuck in the floor
		//
		for ( int i = 1; i < 20; i++ )
		{
			var tryPos = Position + Vector3.Up * i;

			var tr = TraceFromTo( tryPos, Position );
			if ( !tr.StartedSolid )
			{
				Position = tryPos + tr.Direction.Normal * (tr.Distance - 0.5f);
				Velocity = 0;
				return true;
			}
		}

		//
		// Then fuck it, we got to get unstuck some how, try random shit
		//
		for ( int i = 1; i < 100; i++ )
		{
			var tryPos = Position + Vector3.Random * i;

			var tr = TraceFromTo( tryPos, Position );
			if ( !tr.StartedSolid )
			{
				Position = tryPos + tr.Direction.Normal * (tr.Distance - 0.5f);
				Velocity = 0;
				return true;
			}
		}

		return false;
	}
}
