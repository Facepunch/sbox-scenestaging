using Sandbox;
using System;

public sealed class TurretComponent : GameObjectComponent
{
	[Property] GameObject Gun { get; set; }
	[Property] float Speed { get; set; } = 2.0f;

	float turretAngle;

	public override void Update()
	{
		turretAngle += Time.Delta * 360 * Speed;

		Gun.Transform = Gun.Transform.WithRotation( Rotation.From( 0, turretAngle, 0 ) );
	}
}
