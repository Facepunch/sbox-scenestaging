namespace Sandbox;

public class Particle
{
	public Vector3 Position;
	public Vector3 Size;
	public Vector3 Velocity;
	public Color Color;
	public float Alpha;
	public float BornTime;
	public float Age;
	public float Radius;
	public Angles Angles;
	public int Sequence;
	public Vector3 SequenceTime;
	public int Frame;

	public float Random01;
	public float Random02;
	public float Random03;
	public float Random04;
	public float Random05;
	public float Random06;
	public float Random07;

	public Vector3 HitPos;
	public Vector3 HitNormal;
	public float HitTime;
	public float LastHitTime;
	public Vector3 StartPosition;

	public static Queue<Particle> Pool = new( 512 );

	public static Particle Create()
	{
		if ( !Pool.TryDequeue( out Particle p ) )
		{
			p = new Particle();
		}

		p.Random01 = Random.Shared.Float( 0, 1 );
		p.Random02 = Random.Shared.Float( 0, 1 );
		p.Random03 = Random.Shared.Float( 0, 1 );
		p.Random04 = Random.Shared.Float( 0, 1 );
		p.Random05 = Random.Shared.Float( 0, 1 );
		p.Random06 = Random.Shared.Float( 0, 1 );
		p.Random07 = Random.Shared.Float( 0, 1 );

		p.BornTime = Time.Now;
		p.Age = 0;
		p.Angles = Angles.Zero;
		p.Frame = 0;
		p.Velocity = 0;
		p.Color = Color.White;
		p.Alpha = 1;
		p.Sequence = 0;
		p.SequenceTime = 0;
		p.Size = 5;
		p.HitTime = -1000;
		p.LastHitTime = -1000;

		return p;
	}

	public void ApplyDamping( in float amount )
	{
		if ( amount <= 0.00f )
			return;

		var speed = Velocity.Length;
		if ( speed < 100 ) speed = 100;
		var drop = speed * amount;
		float newspeed = speed - drop;

		if ( newspeed < 0 )
			newspeed = 0;

		if ( newspeed == speed )
			return;

		newspeed /= speed;
		Velocity *= newspeed;
	}

	public void MoveWithCollision( in float bounce, in float friction, in float bumpiness, in float push, in bool die, in float dt, float radius, TagSet collisionIgnore )
	{
		const float surfaceOffset = 0.1f;

		// We previously hit something.
		// Keep the surface normal out of our velocity
		// Periodically check whether it's still there.
		if ( HitTime > 0 )
		{
			LastHitTime = Time.Now;

			// if time passed, or we moved too far, see if it's still there
			bool recheck = HitTime < Time.Now - 0.5f || HitPos.Distance( Position ) > 16;

			if ( recheck )
			{
				var checkTrace = global::Physics.Trace.Ray( Position, Position + HitNormal * surfaceOffset * -2.0f )
								.Radius( radius * Radius )
								.WithoutTags( collisionIgnore )
								.Run();

				if ( checkTrace.Hit )
				{
					HitPos = checkTrace.HitPosition;
					HitNormal = checkTrace.Normal;
					HitTime = Time.Now;
				}
				else
				{
					HitTime = 0;
					HitPos = 0;
					HitNormal = 0;
				}
			}

			if ( HitTime > 0 )
			{
				// Keep removing the ground velocity
				Velocity = Velocity.SubtractDirection( HitNormal );
			}

		}

		if ( LastHitTime > Time.Now - 1.0f )
		{
			ApplyDamping( friction * dt * 5.0f );
		}

		var targetPosition = Position + Velocity * dt;

		var tr = global::Physics.Trace.Ray( Position, targetPosition )
										.Radius( radius * Radius )
										.WithoutTags( collisionIgnore )
										.Run();
		if ( !tr.Hit )
		{
			Position = targetPosition;
			return;
		}

		//
		// If we want to die on collision then set its age to max
		//
		if ( die )
		{
			Age = float.MaxValue;
		}

		//
		// If we have push, then push the physics object we hit
		//
		if ( push != 0 )
		{
			tr.Body.ApplyForceAt( tr.HitPosition, Velocity * tr.Body.Mass * push );
		}

		HitPos = tr.HitPosition;
		HitNormal = tr.Normal;
		HitTime = Time.Now;

		var velocity = Velocity;
		var speed = Velocity.Length;

		var surfaceNormal = tr.Normal;

		// make the hit normal bumpy if we have bumpiness
		if ( speed > 10f && bumpiness > 0 )
		{
			surfaceNormal += Vector3.Random * bumpiness * 0.5f;
		}

		var surfaceVelocityNormal = velocity.SubtractDirection( surfaceNormal, 1 + bounce ).Normal;

		targetPosition = tr.EndPosition;// + tr.Normal * surfaceOffset;

		Velocity = surfaceVelocityNormal * speed;

		if ( bounce > 0 && Velocity.Dot( tr.Normal ) > 5.0f )
		{
			HitTime = 0;
		}

		Position = targetPosition;
	}
}
