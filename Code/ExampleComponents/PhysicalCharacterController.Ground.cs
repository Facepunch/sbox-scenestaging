public sealed partial class PhysicalCharacterController : Component
{
	/// <summary>
	/// Enable automatic ground detection. If you disable this then you should be doing
	/// your own ground detection somewhere else.
	/// </summary>
	[Property, ToggleGroup( "Feet" )] public bool Feet { get; set; } = true;
	[Property, Group( "Feet" )] public bool FeetDebug { get; set; } = false;

	GameObject GroundObject { get; set; }
	Component GroundComponent { get; set; }
	float GroundFriction { get; set; }

	TimeUntil timeUntilAllowedGround = 0;

	/// <summary>
	/// Amount of time since this character was last on the ground
	/// </summary>
	public TimeSince TimeSinceGrounded { get; private set; } = 0;

	/// <summary>
	/// Amount of time since this character was last not on the ground
	/// </summary>
	public TimeSince TimeSinceUngrounded { get; private set; } = 0;

	public void PreventGroundingForSeconds( float seconds )
	{
		timeUntilAllowedGround = MathF.Max( timeUntilAllowedGround, seconds );
		UpdateGround( default );
	}


	float skin => 0.001f;

	/// <summary>
	/// Lift player up and place a skin level above the ground
	/// </summary>
	void Reground()
	{
		var currentPosition = WorldPosition;

		var tr = TraceBody( currentPosition + Vector3.Up * StepHeight, currentPosition + Vector3.Down * StepHeight );

		if ( tr.StartedSolid )
			return;

		if ( tr.Hit )
		{
			var targetPosition = tr.EndPosition + Vector3.Up * skin;
			var delta = currentPosition - targetPosition;
			if ( delta == Vector3.Zero ) return;

			WorldPosition = tr.EndPosition + Vector3.Up * skin;

			// when stepping down, clear out the gravity velocity to avoid
			// it thinking we're falling and building up like crazy
			if ( delta.z > 0.1f )
			{
				Body.Velocity = Body.Velocity * 0.9f;
				Body.Velocity = Body.Velocity.WithZ( 0 );
			}
		}
	}

	void CategorizeGround()
	{
		var groundVel = GroundVelocity.z;
		bool wasOnGround = IsOnGround;

		if ( IsSwimming || IsClimbing )
		{
			PreventGroundingForSeconds( 0.1f );
			UpdateGround( default );
			return;
		}

		// ground is pushing us crazy, stop being grounded
		if ( groundVel > 250 )
		{
			PreventGroundingForSeconds( 0.3f );
			UpdateGround( default );
			return;
		}

		var velocity = Velocity - GroundVelocity;
		if ( timeUntilAllowedGround > 0 || groundVel > 300 )
		{
			UpdateGround( default );
			return;
		}

		var testHeight = IsOnGround ? StepHeight : 4;
		var footbox = BBox.FromPositionAndSize( new Vector3( 0, 0, BodyHeight * 0.25f ), new Vector3( BodyRadius, BodyRadius, BodyHeight * 0.5f ) );
		var from = WorldTransform.Position + Vector3.Up * testHeight;
		var to = from + Vector3.Down * testHeight * 2;

		var tr = Scene.Trace.Box( footbox, from, to ).IgnoreGameObjectHierarchy( GameObject ).Run();

		if ( tr.StartedSolid )
		{
			//	footbox = footbox = BBox.FromPositionAndSize( new Vector3( 0, 0, BodyHeight * 0.25f ), new Vector3( 2, 2, BodyHeight * 0.5f ) );
			//	tr = Scene.Trace.Box( footbox, from, to ).IgnoreGameObjectHierarchy( GameObject ).Run();
		}

		if ( tr.StartedSolid )
		{
			UpdateGround( default );
			return;
		}

		if ( !tr.StartedSolid && tr.Hit && CanStandOnSurfaceNormal( tr.Normal ) )
		{
			UpdateGround( tr );
			Reground();

			//
			// Stick to ground
			//
			if ( false && TimeSinceUngrounded > 2f && tr.Distance > testHeight )
			{
				DebugDrawSystem.Current.AddLine( Body.WorldPosition, tr.EndPosition ).WithColor( Color.White ).WithTime( 60 );
				Body.WorldPosition = tr.EndPosition;// + Vector3.Up * 0.1f;
			}

			if ( FeetDebug )
				DebugDrawSystem.Current.AddBox( footbox, new Transform( tr.EndPosition ) ).WithColor( Color.Green );
		}
		else
		{
			UpdateGround( default );

			if ( FeetDebug )
				DebugDrawSystem.Current.AddBox( footbox, new Transform( to ) ).WithColor( Color.Orange );
		}
	}

	/// <summary>
	/// Return true if this surface is less than GroundAngle
	/// </summary>
	public bool CanStandOnSurfaceNormal( Vector3 normal )
	{
		return Vector3.GetAngle( Vector3.Up, normal ) <= GroundAngle;
	}

	void UpdateGround( SceneTraceResult tr )
	{
		var wasGrounded = IsOnGround;

		var body = tr.Body;

		GroundObject = body?.GetGameObject();
		GroundComponent = body?.GetComponent();

		if ( GroundObject is not null )
		{
			TimeSinceGrounded = 0;
			_groundTransform = GroundObject.WorldTransform;
			GroundFriction = tr.Surface.Friction;

			if ( tr.Component is Collider collider )
			{
				if ( collider.Friction.HasValue )
					GroundFriction = collider.Friction.Value;
			}
		}
		else
		{
			TimeSinceUngrounded = 0;
			_groundTransform = default;
		}

		if ( wasGrounded != IsOnGround )
		{
			UpdateBody();
		}
	}
}
