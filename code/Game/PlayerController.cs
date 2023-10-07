using Sandbox;
using System;

public class FixedUpdate
{
	public float Frequency = 60;
	public float MaxSteps = 1;
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

	public Vector3 Velocity { get; set; }
	public bool IsOnGround { get; private set; }
	public float SurfaceFriction { get; private set; }


	FixedUpdate updateManager = new FixedUpdate();

	public override void DrawGizmos()
	{
		Gizmo.Draw.LineBBox( BoundingBox );
	}

	public override void Update()
	{
		updateManager.Run( DoFixedUpdate );
		//DoFixedUpdate();
	}

	void DoFixedUpdate()
	{
		Velocity += ((Vector3.Forward * 100.0f) + (Vector3.Random * 10.0f)) * Time.Delta;
		Velocity -= Gravity * Time.Delta * 0.5f;

		WalkMove();
		CategorizePosition( IsOnGround );

		if ( IsOnGround )
		{
			Velocity = Velocity.WithZ( 0 );
		}
		else
		{
			Velocity -= Gravity * Time.Delta * 0.5f;
		}

		using ( Gizmo.Scope( $"PlayerBBox {GameObject.Id}", GameObject.Transform ) )
		{
			Gizmo.Draw.Color = IsOnGround ? Color.Green.WithAlpha( 0.1f ) : Color.Cyan.WithAlpha( 0.3f );

			Gizmo.Draw.LineBBox( BoundingBox );
		}
	}

	public void WalkMove()
	{
		var currentPos = GameObject.Transform.Position;

		MoveHelper2 helper = new MoveHelper2( Scene.PhysicsWorld, currentPos, Velocity );
		helper.MaxStandableAngle = GroundAngle;
		helper.Trace = helper.Trace.Size( BoundingBox );

		helper.TryMoveWithStep( Time.Delta, StepHeight );

		Velocity = helper.Velocity;
		GameObject.Transform = new Transform( helper.Position );

	//	StayOnGround();
	}

	public void AirMove()
	{
		var currentPos = GameObject.WorldTransform.Position;
		Velocity += Vector3.Forward * 100.0f * Time.Delta;
		Velocity -= Gravity * Time.Delta * 0.5f;

		MoveHelper2 helper = new MoveHelper2( Scene.PhysicsWorld, currentPos, Velocity );
		helper.MaxStandableAngle = GroundAngle;
		helper.Trace = helper.Trace.Size( BoundingBox );

		helper.TryMoveWithStep( Time.Delta, StepHeight );

		Velocity = helper.Velocity;
		//GameObject.WorldTransform = new Transform( helper.Position );

		//StayOnGround();

		Velocity -= Gravity * Time.Delta * 0.5f;
	}

	public void StayOnGround()
	{
		var Position = GameObject.WorldTransform.Position;

		var start = Position + Vector3.Up * 2;
		var end = Position + Vector3.Down * StepHeight;

		// See how far up we can go without getting stuck
		var trace = Scene.PhysicsWorld.Trace.Box( BoundingBox, Position, start ).Run();
		start = trace.EndPosition;

		// Now trace down from a known safe position
		trace = Scene.PhysicsWorld.Trace.Box( BoundingBox, start, end ).Run();

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
			return;
		}

		var pm = Scene.PhysicsWorld.Trace.Box( BoundingBox, vBumpOrigin, point ).Run();

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
			SurfaceFriction = 0.5f;
		}

		if ( bMoveToEndPos && !pm.StartedSolid && pm.Fraction > 0.0f && pm.Fraction < 1.0f )
		{
			GameObject.WorldTransform = new Transform( pm.EndPosition );
		}

	}

	public virtual void ClearGround()
	{
		IsOnGround = false;
		SurfaceFriction = 1.0f;
	}
}
