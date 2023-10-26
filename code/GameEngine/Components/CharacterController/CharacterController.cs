using Sandbox;
using System;
using System.Drawing;

[Title( "Character Controller" )]
[Category( "Physics" )]
[Icon( "directions_walk", "red", "white" )]
[EditorHandle( "materials/gizmo/charactercontroller.png" )]
public class CharacterController : BaseComponent
{
	[Range( 0, 200 )]
	[Property] public float Radius { get; set; } = 16.0f;

	[Range( 0, 200 )]
	[Property] public float Height { get; set; } = 64.0f;

	[Range( 0, 50 )]
	[Property] public float StepHeight { get; set; } = 18.0f;

	[Range( 0, 90 )]
	[Property] public float GroundAngle { get; set; } = 45.0f;

	[Range( 0, 20 )]
	[Property] public float Acceleration { get; set; } = 10.0f;

	[Property] public TagSet IgnoreLayers { get; set; } = new ();

	public BBox BoundingBox => new BBox( new Vector3( -Radius, -Radius, 0 ), new Vector3( Radius, Radius, Height ) );

	public Vector3 Velocity { get; set; }

	public bool IsOnGround { get; set; }

	public override void DrawGizmos()
	{
		Gizmo.Draw.LineBBox( BoundingBox );
	}

	/// <summary>
	/// Add acceleration to the current velocity. 
	/// No need to scale by time delta - it will be done inside.
	/// </summary>
	public void Accelerate( Vector3 vector )
	{
		if ( vector.IsNearZeroLength )
			return;

		Vector3 wishdir = vector.Normal;
		float wishspeed = vector.Length;

		// See if we are changing direction a bit
		var currentspeed = Velocity.Dot( wishdir );

		// Reduce wishspeed by the amount of veer.
		var addspeed = wishspeed - currentspeed;

		// If not going to add any speed, done.
		if ( addspeed <= 0 )
			return;

		// Determine amount of acceleration.
		var accelspeed = Acceleration * Time.Delta * wishspeed;

		// Cap at addspeed
		if ( accelspeed > addspeed )
			accelspeed = addspeed;

		Velocity += wishdir * accelspeed;
	}

	/// <summary>
	/// Apply an amount of friction to the current velocity.
	/// No need to scale by time delta - it will be done inside.
	/// </summary>
	public void ApplyFriction( float frictionAmount, float stopSpeed = 140.0f )
	{
		var speed = Velocity.Length;
		if ( speed < 0.01f ) return;

		// Bleed off some speed, but if we have less than the bleed
		//  threshold, bleed the threshold amount.
		float control = (speed < stopSpeed) ? stopSpeed : speed;

		// Add the amount to the drop amount.
		var drop = control * Time.Delta * frictionAmount;

		// scale the velocity
		float newspeed = speed - drop;
		if ( newspeed < 0 ) newspeed = 0;
		if ( newspeed == speed ) return;

		newspeed /= speed;
		Velocity *= newspeed;
	}

	PhysicsTraceBuilder BuildTrace( Vector3 from, Vector3 to ) => BuildTrace( Scene.PhysicsWorld.Trace.Ray( from, to ) );
	
	PhysicsTraceBuilder BuildTrace( PhysicsTraceBuilder source ) => source.Size( BoundingBox ).WithoutTags( IgnoreLayers );

	void Move( bool step )
	{
		if ( step && IsOnGround )
		{
			Velocity = Velocity.WithZ( 0 );
		}

		if ( Velocity.Length < 0.001f )
		{
			Velocity = Vector3.Zero;
			return;
		}

		var pos = GameObject.Transform.Position;

		var mover = new CharacterControllerHelper( BuildTrace( pos, pos ), pos, Velocity );
		mover.Bounce = 0.3f;
		mover.MaxStandableAngle = GroundAngle;

		if ( step && IsOnGround )
		{
			mover.TryMoveWithStep( Time.Delta, StepHeight );
		}
		else
		{
			mover.TryMove( Time.Delta );
		}

		Transform.Position = mover.Position;
		Velocity = mover.Velocity;
	}

	void CategorizePosition()
	{
		var Position = Transform.Position;
		var point = Position + Vector3.Down * 2;
		var vBumpOrigin = Position;
		var wasOnGround = IsOnGround;

		// We're flying upwards too fast, never land on ground
		if ( !IsOnGround && Velocity.z > 50.0f )
		{
			IsOnGround = false;
			return;
		}

		//
		// trace down one step height if we're already on the ground "step down". If not, search for floor right below us
		// because if we do StepHeight we'll snap that many units to the ground
		//
		point.z -= wasOnGround ? StepHeight : 0.1f;


		var pm = BuildTrace( vBumpOrigin, point ).Run();

		//
		// we didn't hit - or the ground is too steep to be ground
		//
		if ( !pm.Hit || Vector3.GetAngle( Vector3.Up, pm.Normal ) > GroundAngle )
		{
			IsOnGround = false;
			return;
		}

		//
		// we are on ground
		//
		IsOnGround = true;

		//
		// move to this ground position, if we moved, and hit
		//
		if ( wasOnGround && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
		{
			Transform.Position = pm.EndPosition + pm.Normal * 0.01f;
		}
	}

	/// <summary>
	/// Disconnect from ground and punch our velocity. This is useful if you want the player to jump or something.
	/// </summary>
	public void Punch( in Vector3 amount )
	{
		IsOnGround = false;
		Velocity += amount;
	}

	/// <summary>
	/// Move a character, with this velocity
	/// </summary>
	public void Move()
	{
		if ( TryUnstuck() )
			return;

		if ( IsOnGround )
		{
			Move( true );
		}
		else
		{
			Move( false );
		}

		CategorizePosition();
	}

	/// <summary>
	/// Move from our current position to this target position, but using tracing an sliding.
	/// This is good for different control modes like ladders and stuff.
	/// </summary>
	public void MoveTo( Vector3 targetPosition, bool useStep )
	{
		if ( TryUnstuck() )
			return;

		var pos = Transform.Position;
		var delta = targetPosition - pos;

		var mover = new CharacterControllerHelper( BuildTrace( pos, pos ), pos, delta );
		mover.MaxStandableAngle = GroundAngle;

		if ( useStep )
		{
			mover.TryMoveWithStep( 1.0f, StepHeight );
		}
		else
		{
			mover.TryMove( 1.0f );
		}

		Transform.Position = mover.Position;
	}

	int _stuckTries;

	bool TryUnstuck()
	{
		var result = BuildTrace( Transform.Position, Transform.Position ).Run();

		// Not stuck, we cool
		if ( !result.StartedSolid )
		{
			_stuckTries = 0;
			return false;
		}

		//using ( Gizmo.Scope( "unstuck", Transform.World ) )
		//{
		//	Gizmo.Draw.Color = Gizmo.Colors.Red;
		//	Gizmo.Draw.LineBBox( BoundingBox );
		//}

		int AttemptsPerTick = 20;

		for ( int i = 0; i < AttemptsPerTick; i++ )
		{
			var pos = Transform.Position + Vector3.Random.Normal * (((float)_stuckTries) / 2.0f);

			// First try the up direction for moving platforms
			if ( i == 0 )
			{
				pos = Transform.Position + Vector3.Up * 2;
			}

			result = BuildTrace( pos, pos ).Run();

			if ( !result.StartedSolid )
			{
				//Log.Info( $"unstuck after {_stuckTries} tries ({_stuckTries * AttemptsPerTick} tests)" );
				Transform.Position = pos;
				return false;
			}
		}

		_stuckTries++;

		return true;
	}
}
