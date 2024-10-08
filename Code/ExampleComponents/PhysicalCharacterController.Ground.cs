﻿public sealed partial class PhysicalCharacterController : Component
{
	/// <summary>
	/// Enable automatic ground detection. If you disable this then you should be doing
	/// your own ground detection somewhere else.
	/// </summary>
	[Property, ToggleGroup( "Feet" )] public bool Feet { get; set; } = true;
	[Property, Group( "Feet" )] public bool FeetDebug { get; set; } = false;

	GameObject GroundObject { get; set; }
	Component GroundComponent { get; set; }

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

	void CategorizeGround()
	{
		var groundVel = GroundVelocity.z;
		bool wasOnGround = IsOnGround;

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
			UpdateGround( tr.Body );

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

	void UpdateGround( PhysicsBody body )
	{
		var wasGrounded = IsOnGround;

		GroundObject = body?.GetGameObject();
		GroundComponent = body?.GetComponent();

		if ( GroundObject is not null )
		{
			TimeSinceGrounded = 0;
			_groundTransform = GroundObject.WorldTransform;
		}
		else
		{
			TimeSinceUngrounded = 0;
			_groundLocal = default;
			_groundTransform = default;
		}

		if ( wasGrounded != IsOnGround )
		{
			UpdateBody();
		}
	}
}
