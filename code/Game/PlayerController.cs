using Sandbox;
using System;
using System.Drawing;

public class FixedUpdate
{
	public float Frequency = 32;
	public float MaxSteps = 5;
	public float Delta => 1.0f / Frequency;

	float lastTime;

	internal void Run( Action fixedUpdate )
	{
		var saveNow = Time.Now;
		var saveDelta = Time.Delta;

		var delta = Delta;
		var time = RealTime.Now;
		lastTime = lastTime.Clamp( time - (MaxSteps * delta), time + delta );

		

		Time.Delta = delta;

		while ( lastTime < time )
		{
			Time.Now = lastTime;

			fixedUpdate();
			
			lastTime += delta;
		}

		Time.Now = saveNow;
		Time.Delta = saveDelta;
	}
}


public class PlayerController : BaseComponent
{
	[Property] public BBox BoundingBox { get; set; }
	[Property] public float StepHeight { get; set; } = 18.0f;
	[Property] public float GroundAngle { get; set; } = 45.0f;
	[Property] public Vector3 Gravity { get; set; } = new Vector3( 0, 0, 800 );

	[Property] public float Acceleration { get; set; } = 10.0f;
	[Property] public float AirAcceleration { get; set; } = 50.0f;
	[Property] public float AirControl { get; set; } = 30.0f;
	[Property] public float StopSpeed { get; set; } = 100.0f;
	[Property] public float GroundFriction { get; set; } = 4.0f;
	[Property] public float CameraDistance { get; set; } = 200.0f;

	public Vector3 Velocity { get; set; }
	public bool IsOnGround { get; private set; }
	public float SurfaceFriction { get; private set; }
	public Vector3 WishVelocity { get; private set; }


	[Property] GameObject Body { get; set; }
	[Property] GameObject Eye { get; set; }

	public Angles EyeAngles;

	FixedUpdate updateManager = new FixedUpdate();

	public override void DrawGizmos()
	{
		Gizmo.Draw.LineBBox( BoundingBox );
	}

	public override void Update()
	{
		//updateManager.Run( DoFixedUpdate );



		using ( Gizmo.Scope( $"PlayerBBox {GameObject.Id}", GameObject.Transform.WithRotation( Rotation.Identity )  ) )
		{
			Gizmo.Draw.Color = IsOnGround ? Color.Green.WithAlpha( 0.1f ) : Color.Cyan.WithAlpha( 0.3f );
			Gizmo.Draw.LineBBox( BoundingBox );
		}

		EyeAngles.pitch += Input.MouseDelta.y * 0.1f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
		EyeAngles.roll = 0;

		var camera = GameObject.GetComponent<CameraComponent>( true, true );
		if ( camera is not null )
		{
			var camPos = Eye.WorldTransform.Position - EyeAngles.ToRotation().Forward * CameraDistance;
			camera.GameObject.WorldTransform = camera.GameObject.WorldTransform.WithPosition( camPos, EyeAngles.ToRotation() );
		}

		if ( Body is not null )
		{
			Body.Transform = Body.Transform.WithRotation( new Angles( 0, EyeAngles.yaw, 0 ).ToRotation() );
		}


		DoFixedUpdate();
	}

	void DoFixedUpdate()
	{
		if ( IsOnGround )
		{
			Velocity = Velocity.WithZ( 0 );
			ApplyFriction( GroundFriction * SurfaceFriction );
		}

		HandleInput();

		Velocity -= Gravity * Time.Delta * 0.5f;
		bool wasOnGround = IsOnGround;

		if ( IsOnGround )
		{
			WalkMove();
		}
		else
		{
			AirMove();
		}

		CategorizePosition( wasOnGround );

		if ( IsOnGround )
		{
			Velocity = Velocity.WithZ( 0 );
		}
		else
		{
			Velocity -= Gravity * Time.Delta * 0.5f;
		}
	}

	/// <summary>
	/// Remove ground friction from velocity
	/// </summary>
	public virtual void ApplyFriction( float frictionAmount = 1.0f )
	{
		// If we are in water jump cycle, don't apply friction
		//if ( player->m_flWaterJumpTime )
		//   return;

		// Not on ground - no friction


		// Calculate speed
		var speed = Velocity.Length;
		if ( speed < 0.1f ) return;

		// Bleed off some speed, but if we have less than the bleed
		//  threshold, bleed the threshold amount.
		float control = (speed < StopSpeed) ? StopSpeed : speed;

		// Add the amount to the drop amount.
		var drop = control * Time.Delta * frictionAmount;

		// scale the velocity
		float newspeed = speed - drop;
		if ( newspeed < 0 ) newspeed = 0;

		if ( newspeed != speed )
		{
			newspeed /= speed;
			Velocity *= newspeed;
		}

		// mv->m_outWishVel -= (1.f-newspeed) * mv->m_vecVelocity;
	}

	public void HandleInput()
	{
		var rot = EyeAngles.ToRotation();

		WishVelocity = 0;

		if ( Input.Down( "Forward" ) ) WishVelocity += rot.Forward;
		if ( Input.Down( "Backward" ) ) WishVelocity += rot.Backward;
		if ( Input.Down( "Left" ) ) WishVelocity += rot.Left;
		if ( Input.Down( "Right" ) ) WishVelocity += rot.Right;

		if ( !WishVelocity.IsNearlyZero() ) WishVelocity = WishVelocity.Normal;

		if ( Input.Down( "Run" ) ) WishVelocity *= 320.0f;
		else WishVelocity *= 150.0f;

		if( IsOnGround && Input.Down( "Jump" ) )
		{
			float flGroundFactor = 1.0f;
			float flMul = 268.3281572999747f * 1.2f;
			float startz = Velocity.z;

			//if ( Duck.IsActive )
			//	flMul *= 0.8f;

			Velocity = Velocity.WithZ( startz + flMul * flGroundFactor );

			IsOnGround = false;
		}

	//	var wishdir = WishVelocity.Normal;
		//var wishspeed = WishVelocity.Length;

	//	WishVelocity = WishVelocity.WithZ( 0 );
	//	WishVelocity = WishVelocity.Normal * wishspeed;

	//	Accelerate( wishdir, wishspeed, 0, Acceleration );

		//Velocity += wishVelocity * Time.Delta * 100.0f;




	}

	public virtual void Accelerate( Vector3 wishdir, float wishspeed, float speedLimit, float acceleration )
	{
		if ( speedLimit > 0 && wishspeed > speedLimit )
			wishspeed = speedLimit;

		// See if we are changing direction a bit
		var currentspeed = Velocity.Dot( wishdir );

		// Reduce wishspeed by the amount of veer.
		var addspeed = wishspeed - currentspeed;

		// If not going to add any speed, done.
		if ( addspeed <= 0 )
			return;

		// Determine amount of acceleration.
		var accelspeed = acceleration * Time.Delta * wishspeed * SurfaceFriction;

		// Cap at addspeed
		if ( accelspeed > addspeed )
			accelspeed = addspeed;

		Velocity += wishdir * accelspeed;
	}

	public virtual void AirMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		Accelerate( wishdir, wishspeed, AirControl, AirAcceleration );
		Move( false );
	}

	public virtual void WalkMove()
	{
		var wishdir = WishVelocity.Normal;
		var wishspeed = WishVelocity.Length;

		WishVelocity = WishVelocity.WithZ( 0 );
		WishVelocity = WishVelocity.Normal * wishspeed;

		Velocity = Velocity.WithZ( 0 );
		Accelerate( wishdir, wishspeed, 0, Acceleration );
		Velocity = Velocity.WithZ( 0 );

		if ( Velocity.Length < 1.0f )
		{
			Velocity = Vector3.Zero;
			return;
		}

		//var pos = GameObject.Transform.Position;

		// first try just moving to the destination
		//var dest = (pos + Velocity * Time.Delta).WithZ( pos.z );

		//if ( TryMove( dest ) )
		//	return;

		Move( true );

	//	Velocity = Velocity.Normal * MathF.Min( Velocity.Length, wishspeed );
	}

	PhysicsTraceBuilder BuildTrace( Vector3 from, Vector3 to )
	{
		var tr = Scene.PhysicsWorld.Trace.Ray( from, to );
		return BuildTrace( tr );
	}

	PhysicsTraceBuilder BuildTrace( PhysicsTraceBuilder source )
	{
		return source.Size( BoundingBox );
	}

	public virtual bool TryMove( Vector3 target )
	{
		var mover = new MoveHelper2( Scene.PhysicsWorld, GameObject.Transform.Position, Velocity );
		mover.Trace = BuildTrace( mover.Trace );
		mover.MaxStandableAngle = GroundAngle;

		var tr = mover.TraceFromTo( GameObject.Transform.Position, target );

		if ( tr.Hit ) return false;

		GameObject.Transform = GameObject.Transform.WithPosition( tr.EndPosition );
		return true;
	}

	public virtual void Move( bool step )
	{
		var mover = new MoveHelper2( Scene.PhysicsWorld, GameObject.Transform.Position, Velocity );
		mover.Trace = BuildTrace( mover.Trace );
		mover.MaxStandableAngle = GroundAngle;

		if ( step )
		{
			mover.TryMoveWithStep( Time.Delta, StepHeight );
		}
		else
		{
			mover.TryMove( Time.Delta );
		}

		GameObject.Transform = GameObject.Transform.WithPosition( mover.Position );
		Velocity = mover.Velocity;
	}

	public void StayOnGround()
	{
		var Position = GameObject.WorldTransform.Position;

		var start = Position + Vector3.Up * 2;
		var end = Position + Vector3.Down * StepHeight;

		// See how far up we can go without getting stuck
		var trace = BuildTrace( Position, start ).Run();
		start = trace.EndPosition;

		// Now trace down from a known safe position
		trace = BuildTrace( start, end ).Run();

		if ( trace.Fraction <= 0 ) return;
		if ( trace.Fraction >= 1 ) return;
		if ( trace.StartedSolid ) return;
		if ( Vector3.GetAngle( Vector3.Up, trace.Normal ) > GroundAngle ) return;

		// This is incredibly hacky. The real problem is that trace returning that strange value we can't network over.
		// float flDelta = fabs( mv->GetAbsOrigin().z - trace.m_vEndPos.z );
		// if ( flDelta > 0.5f * DIST_EPSILON )

		GameObject.WorldTransform = GameObject.Transform.WithPosition( trace.EndPosition );
	}

	public virtual void CategorizePosition( bool bStayOnGround )
	{
		SurfaceFriction = 1.0f;
		float MaxNonJumpVelocity = 140.0f;
		bool Swimming = false;

		var Position = GameObject.WorldTransform.Position;

		// Doing this before we move may introduce a potential latency in water detection, but
		// doing it after can get us stuck on the bottom in water if the amount we move up
		// is less than the 1 pixel 'threshold' we're about to snap to.	Also, we'll call
		// this several times per frame, so we really need to avoid sticking to the bottom of
		// water on each call, and the converse case will correct itself if called twice.
		//CheckWater();

		var point = Position + Vector3.Down * 2;
		var vBumpOrigin = Position;

		//
		//  Shooting up really fast.  Definitely not on ground trimed until ladder shit
		//
		bool bMovingUpRapidly = Velocity.z > MaxNonJumpVelocity;
		bool bMovingUp = Velocity.z > 0;

		bool bMoveToEndPos = false;

		if ( IsOnGround ) // and not underwater
		{
			bMoveToEndPos = true;
			point.z -= StepHeight;
		}
		else if ( bStayOnGround )
		{
			bMoveToEndPos = true;
			point.z -= StepHeight;
		}

		if ( bMovingUpRapidly || Swimming ) // or ladder and moving up
		{
			ClearGround();
			SurfaceFriction = 0;
			return;
		}

		var pm = BuildTrace( vBumpOrigin, point ).Run();

		if ( !pm.Hit || Vector3.GetAngle( Vector3.Up, pm.Normal ) > GroundAngle )
		{
			ClearGround();
			bMoveToEndPos = false;

			if ( Velocity.z > 0 )
				SurfaceFriction = 0.25f;
		}
		else
		{
			IsOnGround = true;
			SurfaceFriction = 1f;
		}

		if ( bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
		{
			GameObject.Transform = GameObject.Transform.WithPosition( pm.EndPosition );
		}

		Gizmo.Draw.ScreenText( $"SurfaceFriction: {SurfaceFriction}\n", 20 );

	}

	public virtual void ClearGround()
	{
		IsOnGround = false;
		SurfaceFriction = 1.0f;
	}
}
