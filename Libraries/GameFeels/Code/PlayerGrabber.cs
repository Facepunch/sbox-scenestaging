using Sandbox;
using System;

public class PlayerGrabber : Component
{
	[Property] public GameObject ImpactEffect { get; set; }
	[Property] public GameObject DecalEffect { get; set; }
	[Property] public float ShootDamage { get; set; } = 9.0f;

	/// <summary>
	/// The higher this is, the "looser" the grip is when dragging objects
	/// </summary>
	[Property, Range( 1, 16 )] public float MovementSmoothness { get; set; } = 3.0f;

	PhysicsBody grabbedBody;
	Transform grabbedOffset;
	Vector3 localOffset;

	bool waitForUp = false;

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		Transform aimTransform = Scene.Camera.Transform.World;

		if ( waitForUp && Input.Down( "attack1" ) )
		{
			return;
		}

		waitForUp = false;

		if ( grabbedBody is not null )
		{
			if ( Input.Down( "attack2" ) )
			{
				grabbedBody.MotionEnabled = false;
				grabbedBody.Velocity = 0;
				grabbedBody.AngularVelocity = 0;

				grabbedOffset = default;
				grabbedBody = default;
				waitForUp = true;
				return;
			}

			var targetTx = aimTransform.ToWorld( grabbedOffset );

			var worldStart = grabbedBody.GetLerpedTransform( Time.Now ).PointToWorld( localOffset );
			var worldEnd = targetTx.PointToWorld( localOffset );

			//var delta = Scene.Camera.Transform.World.PointToWorld( new Vector3( 0, -10, -5 ) ) - worldStart;
			var delta = worldEnd - worldStart;
			for ( var f = 0.0f; f < delta.Length; f += 2.0f )
			{
				var size = 1 - f * 0.01f;
				if ( size < 0 ) break;

				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.SolidSphere( worldStart + delta.Normal * f, size );
			}

			if ( !Input.Down( "attack1" ) )
			{
				grabbedOffset = default;
				grabbedBody = default;
			}
			else
			{
				return;
			}
		}

		if ( Input.Down( "attack2" ) )
		{
			Shoot();
			return;
		}

		var tr = Scene.Trace.Ray( Scene.Camera.Transform.Position, Scene.Camera.Transform.Position + Scene.Camera.Transform.Rotation.Forward * 1000 )
			.Run();

		if ( !tr.Hit || tr.Body is null || tr.Body.BodyType != PhysicsBodyType.Dynamic )
			return;

		if ( Input.Down( "attack1" ) )
		{
			grabbedBody = tr.Body;
			localOffset = tr.Body.Transform.PointToLocal( tr.HitPosition );
			grabbedOffset = aimTransform.ToLocal( tr.Body.Transform );
			grabbedBody.MotionEnabled = true;
		}

	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		Transform aimTransform = Scene.Camera.Transform.World;

		if ( waitForUp && Input.Down( "attack1" ) )
		{
			return;
		}

		waitForUp = false;

		if ( grabbedBody is not null )
		{
			if ( Input.Down( "attack1" ) )
			{
				var targetTx = aimTransform.ToWorld( grabbedOffset );
				grabbedBody.SmoothMove( targetTx, Time.Delta * MovementSmoothness, Time.Delta );
				return;
			}
		}
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();


		if ( grabbedBody is null )
		{
			var tr = Scene.Trace.Ray( Scene.Camera.ScreenNormalToRay( 0.5f ), 1000.0f )
							.Run();

			if ( tr.Hit )
			{
				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.SolidSphere( tr.HitPosition, 1 );
			}
		}
	}

	SoundEvent shootSound = Cloud.SoundEvent( "mdlresrc.toolgunshoot" );

	TimeSince timeSinceShoot;

	public void Shoot()
	{
		if ( timeSinceShoot < 0.1f )
			return;

		timeSinceShoot = 0;

		Sound.Play( shootSound, Transform.Position );

		var ray = Scene.Camera.ScreenNormalToRay( 0.5f );
		ray.Forward += Vector3.Random * 0.03f;

		var tr = Scene.Trace.Ray( ray, 3000.0f )
				.Run();

		if ( !tr.Hit || tr.GameObject is null )
			return;

		if ( ImpactEffect is not null )
		{
			ImpactEffect.Clone( new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( tr.Normal ) ) );
		}

		if ( DecalEffect is not null )
		{
			var decal = DecalEffect.Clone( new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( -tr.Normal, Vector3.Random ), Random.Shared.Float( 0.8f, 1.2f ) ) );
			decal.SetParent( tr.GameObject );
		}

		if ( tr.Body is not null )
		{
			tr.Body.ApplyImpulseAt( tr.HitPosition, tr.Direction * 200.0f * tr.Body.Mass.Clamp( 0, 200 ) );
		}

		var damage = new DamageInfo( ShootDamage, GameObject, GameObject, tr.Hitbox );
		damage.Position = tr.HitPosition;
		damage.Shape = tr.Shape;

		foreach ( var damageable in tr.GameObject.Components.GetAll<IDamageable>() )
		{
			damageable.OnDamage( damage );
		}
	}
}
