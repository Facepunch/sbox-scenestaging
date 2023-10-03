using Sandbox;
using System;

public sealed class TurretComponent : GameObjectComponent
{
	[Property] GameObject Gun { get; set; }

	float turretYaw;
	float turretPitch;

	public override void Update()
	{
		// rotate gun using mouse input
		turretYaw -= Input.MouseDelta.x * 0.1f;
		turretPitch += Input.MouseDelta.y * 0.1f;
		turretPitch = turretPitch.Clamp( -30, 30 );
		Gun.Transform = Gun.Transform.WithRotation( Rotation.From( 0, turretYaw, turretPitch ) );

		// drive tank
		Vector3 movement = 0;
		if ( Input.Down( "Forward" ) ) movement += GameObject.Transform.Rotation.Forward;
		if ( Input.Down( "backward" ) ) movement += GameObject.Transform.Rotation.Backward;

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
	}
}
