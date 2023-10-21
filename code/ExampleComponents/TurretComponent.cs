using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using System;
using System.Threading;

public sealed class TurretComponent : BaseComponent
{
	[Property] GameObject Gun { get; set; }
	[Property] GameObject Bullet { get; set; }
	[Property] GameObject SecondaryBullet { get; set; }
	[Property] GameObject Muzzle { get; set; }

	[Property] ModelComponent GunModel { get; set; }
	[Property] Gradient GunColorGradient { get; set; }
	[Property] Curve GunSizeCurve { get; set; }

	float turretYaw;
	float turretPitch;

	TimeSince timeSinceLastSecondary;

	TimeSince timeSincePrimary = 10;

	void FlashGunModel()
	{
		if ( GunModel is null ) return;
		if ( timeSincePrimary < 0 ) return;

		GunModel.Tint = GunColorGradient.Evaluate( timeSincePrimary * 2.0f );
		GunModel.Transform.LocalScale = GunSizeCurve.Evaluate( timeSincePrimary * 2.0f );
	}

	public override void Update()
	{
		FlashGunModel();

		// rotate gun using mouse input
		turretYaw -= Input.MouseDelta.x * 0.1f;
		turretPitch += Input.MouseDelta.y * 0.1f;
		turretPitch = turretPitch.Clamp( -30, 30 );
		Gun.Transform.Rotation = Rotation.From( turretPitch, turretYaw, 0 );

		var bbox = new BBox( 0, 5 );

		// drive tank
		Vector3 movement = 0;
		if ( Input.Down( "Forward" ) ) movement += Transform.World.Forward;
		if ( Input.Down( "backward" ) ) movement += Transform.World.Backward;

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

		Transform.Local = new Transform( pos, rot, 1 );

		if ( Input.Pressed( "Attack1" ) )
		{
			Assert.NotNull( Bullet );

			var obj = SceneUtility.Instantiate( Bullet, Muzzle.Transform.Position, Muzzle.Transform.Rotation );
			var physics = obj.GetComponent<PhysicsComponent>( true, true );
			if ( physics is not null )
			{
				physics.Velocity = Muzzle.Transform.Rotation.Forward * 2000.0f;
			}

			Stats.Increment( "balls_fired", 1 );

			// Testing sound
			Sound.FromWorld( "rust_smg.shoot", Transform.Position );
			timeSincePrimary = 0;
		}

		var tr = Physics.Trace
			.Ray( Muzzle.Transform.Position + Muzzle.Transform.Rotation.Forward * 50.0f, Muzzle.Transform.Position + Muzzle.Transform.Rotation.Forward * 4000 )
			.Size( bbox )
			//.Radius( 40 )
			.Run();
		/*
		Gizmo.Transform = global::Transform.Zero;
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Line( tr.StartPosition, tr.EndPosition );
		Gizmo.Draw.Line( tr.HitPosition, tr.HitPosition + tr.Normal * 30.0f );

		Gizmo.Draw.LineSphere( new Sphere( tr.HitPosition, 10.0f ) );

		if ( tr.Body is not null )
		{
			var closestPos = tr.Body.FindClosestPoint( tr.HitPosition );
			Gizmo.Draw.LineSphere( new Sphere( closestPos, 10.0f ) );
		}

		var box = bbox;
		box.Mins += tr.EndPosition;
		box.Maxs += tr.EndPosition;

		Gizmo.Draw.LineBBox( box );
				*/
		//	Gizmo.Draw.Text( $"{tr.EndPosition}", Muzzle.WorldTransform );
		//	Gizmo.Draw.Text( $"{tr.HitPosition}\n{tr.Fraction}\n{tr.Direction}", new Transform( tr.HitPosition ) );

		using ( Gizmo.Scope( "circle", new Transform( tr.HitPosition, Rotation.LookAt( tr.Normal ) ) ) )
		{
		//	Gizmo.Draw.LineCircle( 0, 30 );
		}

		if ( Input.Pressed( "Attack2" ) && timeSinceLastSecondary > 0.02f && tr.Hit )
		{
			Stats.Increment( "cubes_fired", 1 );

			timeSinceLastSecondary = 0;

			int i = 0;

			var r = Muzzle.Transform.Rotation;

			for ( float f = 0; f < tr.Distance; f += 10.0f )
			{
				if ( i++ > 200 )
					break;

				var off = MathF.Sin( i * 0.4f ) * r.Right * 20.0f;
				off += MathF.Cos( i * 0.4f ) * r.Up * 20.0f;

				var obj = SceneUtility.Instantiate( SecondaryBullet, tr.StartPosition + tr.Direction * f + off * 0.1f, r );

				//r *= Rotation.From( 2, 4, 2 );

				var physics = obj.GetComponent<PhysicsComponent>( true, true );
				if ( physics is not null )
				{
					physics.Velocity = off * 2.0f;// Muzzle.WorldTransform.Rotation.Forward * 300.0f;
				}
			}
	
		}
	}
}
