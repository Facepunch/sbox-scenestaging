using Sandbox;
using Sandbox.Services;
using System;

public sealed class TurretComponent : GameObjectComponent
{
	[Property] GameObject Gun { get; set; }
	[Property] GameObject Bullet { get; set; }
	[Property] GameObject SecondaryBullet { get; set; }
	[Property] GameObject Muzzle { get; set; }

	float turretYaw;
	float turretPitch;

	TimeSince timeSinceLastSecondary;

	public override void Update()
	{
		// rotate gun using mouse input
		turretYaw -= Input.MouseDelta.x * 0.1f;
		turretPitch += Input.MouseDelta.y * 0.1f;
		turretPitch = turretPitch.Clamp( -30, 30 );
		Gun.Transform = Gun.Transform.WithRotation( Rotation.From( turretPitch, turretYaw, 0  ) );

		// drive tank
		Vector3 movement = 0;
		if ( Input.Down( "Forward" ) ) movement += GameObject.WorldTransform.Forward;
		if ( Input.Down( "backward" ) ) movement += GameObject.WorldTransform.Backward;

		var rot = GameObject.Transform.Rotation;
		var pos = GameObject.Transform.Position + movement * Time.Delta * 100.0f;

		if ( Input.Down( "Left" ) )
		{
			rot *= Rotation.From( 0, Time.Delta * 90.0f, 0 );
		}

		if ( Input.Down( "Right" ) )
		{
			rot *= Rotation.From( 0, Time.Delta * -90.0f, 0 );
		}

		GameObject.Transform = new Transform( pos, rot, 1 );

		if ( Input.Pressed( "Attack1" ) )
		{
			var obj = SceneUtility.Instantiate( Bullet, Muzzle.WorldTransform.Position, Muzzle.WorldTransform.Rotation );
			var physics = obj.GetComponent<PhysicsComponent>( true, true );
			if ( physics is not null )
			{
				physics.Velocity = Muzzle.WorldTransform.Rotation.Forward * 2000.0f;
			}

			Stats.Increment( "balls_fired", 1 );
		}

		if ( Input.Down( "Attack2" ) && timeSinceLastSecondary > 0.02f )
		{
			Stats.Increment( "cubes_fired", 1 );

			timeSinceLastSecondary = 0;

			var obj = SceneUtility.Instantiate( SecondaryBullet, Muzzle.WorldTransform.Position, Muzzle.WorldTransform.Rotation );
			var physics = obj.GetComponent<PhysicsComponent>( true, true );
			if ( physics is not null )
			{
				physics.Velocity = Muzzle.WorldTransform.Rotation.Forward * 300.0f;
			}

		}
	}
}
