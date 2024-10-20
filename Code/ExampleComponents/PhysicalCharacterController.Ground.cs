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


	float skin => 0.05f;

	/// <summary>
	/// Lift player up and place a skin level above the ground
	/// </summary>
	void Reground()
	{
		if ( !IsOnGround )
			return;

		var currentPosition = WorldPosition;

		float radiusScale = 1.0f;
		var tr = TraceBody( currentPosition + Vector3.Up * StepHeight, currentPosition + Vector3.Down * StepHeight, radiusScale );

		while ( tr.StartedSolid )
		{
			radiusScale = radiusScale - 0.1f;
			if ( radiusScale < 0.7f )
				return;

			tr = TraceBody( currentPosition + Vector3.Up * StepHeight, currentPosition + Vector3.Down * StepHeight, radiusScale );
		}

		if ( tr.StartedSolid )
		{
			return;
		}

		if ( tr.Hit )
		{
			var targetPosition = tr.EndPosition + Vector3.Up * skin;
			var delta = currentPosition - targetPosition;
			if ( delta == Vector3.Zero ) return;

			WorldPosition = targetPosition;

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
		var from = WorldPosition + Vector3.Up * testHeight;
		var to = WorldPosition + Vector3.Down * testHeight;

		float radiusScale = 1;
		var tr = TraceBody( from, to, radiusScale );

		while ( tr.StartedSolid || (tr.Hit && !CanStandOnSurfaceNormal( tr.Normal )) )
		{
			radiusScale = radiusScale - 0.1f;
			if ( radiusScale < 0.7f )
			{
				UpdateGround( default );
				return;
			}

			tr = TraceBody( from, to, radiusScale );
		}

		if ( tr.StartedSolid )
		{
			UpdateGround( default );
			return;
		}

		if ( !tr.StartedSolid && tr.Hit && CanStandOnSurfaceNormal( tr.Normal ) )
		{
			DebugDrawSystem.Current.Normal( tr.EndPosition, tr.Normal * 10, Color.Green, 10 );

			UpdateGround( tr );
			Reground();
		}
		else
		{
			UpdateGround( default );
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
