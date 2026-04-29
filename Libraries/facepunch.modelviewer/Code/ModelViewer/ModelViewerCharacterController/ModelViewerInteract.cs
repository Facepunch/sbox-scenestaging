
using Sandbox;

public sealed class ModelViewerInteract : Component
{
	[Property] private GameObject Eye { get; set; }

	[Property] private GameObject Camera { get; set; }
	[Property] public float ShootDamage { get; set; } = 9.0f;

	public GameObject currentlyCarriedObject = null;

	private float distance = 0;
	private Rotation localRotation;
	private Vector3 localMassCenter;

	protected override void OnStart()
	{
		base.OnStart();
	}
	SoundEvent shootSound = Cloud.SoundEvent( "mdlresrc.toolgunshoot" );

	[Property] public GameObject ImpactEffect { get; set; }
	[Property] public GameObject DecalEffect { get; set; }

	[Property] public ModelViewerPlayerController PlayerController { get; set; }

	protected override void OnUpdate()
	{
		Eye.WorldRotation = Camera.WorldRotation;

		var tr = Scene.PhysicsWorld.Trace.Ray( Eye.WorldPosition, Eye.WorldPosition + Eye.WorldRotation.Forward * 500 )
			.WithoutTags( "player" )
			.Run();

		if ( PlayerController.CurrentCameraMode != ModelViewerPlayerController.CameraMode.Character ) return;

		if ( Input.Pressed( "attack2" ) && currentlyCarriedObject == null )
		{
			var snd = Sound.Play( shootSound, WorldPosition );
			snd.Volume = 0.25f;

			if ( tr.Body.IsValid() )
			{
				var breakobject = tr.Body.GetGameObject();
				var damage = new DamageInfo( ShootDamage, GameObject, GameObject );

				if ( tr.Body is not null )
				{
					tr.Body.ApplyImpulseAt( tr.HitPosition, tr.Direction * 200.0f * tr.Body.Mass.Clamp( 0, 200 ) );

					//Sound.Play( tr.Surface.Sounds.Bullet, WorldPosition );

					if ( ImpactEffect is not null )
					{
						ImpactEffect.Clone( new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( tr.Normal ) ) );
					}

					if ( DecalEffect is not null )
					{
						var decal = DecalEffect.Clone( new Transform( tr.HitPosition + tr.Normal * 2.0f, Rotation.LookAt( -tr.Normal, Vector3.Random ), Random.Shared.Float( 0.8f, 1.2f ) ) );
						decal.SetParent( tr.Body.GetGameObject() );
					}

				}

				foreach ( var damageable in breakobject.Components.GetAll<IDamageable>() )
				{
					damageable.OnDamage( damage );
				}
			}
		}

		if ( Input.Pressed( "use" ) )
		{
			if ( tr.Body.IsValid() )
			{
				var interactedObject = tr.Body.GetGameObject();
				var interactComponent = interactedObject.Components.Get<ModelRenderer>();
				var physicsComponent = interactedObject.Components.Get<Rigidbody>();

				if ( interactComponent != null && physicsComponent.IsValid() )
				{
					// Pick up the object
					currentlyCarriedObject = interactedObject;

					// Get the local rotation so we can convert it back to a global target rotation
					localRotation = Eye.WorldRotation.Inverse * physicsComponent.WorldRotation;

					// Keep the mass center to remove it from target position
					localMassCenter = tr.Body.LocalMassCenter;

					// Distance from the eye to mass center
					distance = Eye.WorldPosition.Distance( tr.Body.MassCenter );

					targetRotation = Eye.WorldRotation.Inverse * physicsComponent.WorldRotation;
					HoldRot = physicsComponent.WorldRotation;
				}
			}
			else
			{
				Sound.Play( "player_used" );
			}
		}

		distance += Input.MouseWheel.y * 5;
		distance = Math.Clamp( distance, 20, 500 );

		if ( Input.Released( "use" ) )
		{
			if ( currentlyCarriedObject != null )
			{
				// Release the object
				currentlyCarriedObject = null;
				PlayerController.IsHoldingObject = false;
			}
		}

		if ( Input.Pressed( "attack1" ) )
		{
			if ( currentlyCarriedObject != null )
			{
				//throw the object
				var physicsBody = currentlyCarriedObject.Components.Get<Rigidbody>();
				physicsBody.Velocity = Eye.WorldRotation.Forward * 500;
				physicsBody.AngularVelocity = Vector3.Zero;
				// Release the object
				currentlyCarriedObject = null;
				PlayerController.IsHoldingObject = false;
			}
		}
	
	}


	Rotation targetRotation;
	Rotation HoldRot;
	float RotateSnapAt = 45.0f;
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( !currentlyCarriedObject.IsValid() )
			return;

		var physicsBody = currentlyCarriedObject.Components.Get<Rigidbody>();
		if ( !physicsBody.IsValid() )
			return;

		// Target position is a distance away from the eye, minus center of mass offset
		var currentPosition = physicsBody.WorldPosition;
		var targetPosition = Eye.WorldPosition + Eye.WorldRotation.Forward * distance;
		targetPosition -= physicsBody.WorldRotation * localMassCenter;

		// Calculate the velocity needed to move from current to target position
		var velocity = physicsBody.Velocity;
		Vector3.SmoothDamp( currentPosition, targetPosition, ref velocity, 0.2f, Time.Delta );
		physicsBody.Velocity = velocity;

		// Add the eye rotation back onto the local rotation to make it a global rotation
		var currentRotation = physicsBody.WorldRotation;
		//targetRotation = Eye.WorldRotation * localRotation;

		var eyerot = Rotation.From( new Angles( 0.0f, Camera.WorldRotation.y, 0.0f ) );

		if ( Input.Down( "attack2" ) && currentlyCarriedObject != null )
		{
			PlayerController.IsHoldingObject = true;
			DoRotation( eyerot, Input.MouseDelta );
		}
		else
		{
			PlayerController.IsHoldingObject = false;
		}

		HoldRot = Camera.WorldRotation * targetRotation;

		if ( Input.Down( "run" ) )
		{
			var angles = HoldRot.Angles();

			HoldRot = Rotation.From(
				MathF.Round( angles.pitch / RotateSnapAt ) * RotateSnapAt,
				MathF.Round( angles.yaw / RotateSnapAt ) * RotateSnapAt,
				MathF.Round( angles.roll / RotateSnapAt ) * RotateSnapAt
			);
		}

		// Calculate the velocity needed to move from current to target rotation
		var angvelocity = physicsBody.AngularVelocity;
		Rotation.SmoothDamp( currentRotation, HoldRot, ref angvelocity, 0.075f, Time.Delta );
		physicsBody.AngularVelocity = angvelocity;
	}

	public void DoRotation( Rotation eye, Vector3 input )
	{
		var localRot = eye;
		localRot *= Rotation.FromAxis( Vector3.Up, input.x * 0.125f );
		localRot *= Rotation.FromAxis( Vector3.Right, input.y * 0.125f );
		localRot = eye.Inverse * localRot;

		targetRotation = localRot * targetRotation;
	}
}
