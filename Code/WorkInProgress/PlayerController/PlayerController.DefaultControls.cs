namespace Sandbox;

public sealed partial class PlayerController : Component
{
	/// <summary>
	/// The direction we're looking.
	/// </summary>
	[Sync]
	public Angles EyeAngles { get; set; }

	/// <summary>
	/// The player's eye position, in first person mode
	/// </summary>
	public Vector3 EyePosition => WorldPosition + Vector3.Up * (BodyHeight - EyeDistanceFromTop);

	/// <summary>
	/// The player's eye position, in first person mode
	/// </summary>
	public Transform EyeTransform => new Transform( EyePosition, EyeAngles, 1 );

	[Sync]
	public bool IsDucking { get; set; }

	/// <summary>
	/// The distance from the top of the head to to closest ceiling
	/// </summary>
	public float Headroom { get; set; }


	protected override void OnUpdate()
	{
		UpdateGroundEyeRotation();

		if ( Scene.IsEditor )
			return;

		if ( !IsProxy )
		{
			if ( UseInputControls )
			{
				UpdateEyeAngles();
			}

			if ( UseCameraControls )
			{
				UpdateCameraPosition();
			}
		}

		UpdateVisibility();

		if ( UseAnimatorControls && Renderer.IsValid() )
		{
			UpdateAnimation( Renderer );
		}
	}


	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor ) return;

		UpdateHeadroom();
		UpdateFalling();

		if ( IsProxy ) return;
		if ( !UseInputControls ) return;

		InputMove();
		UpdateDucking( Input.Down( "duck" ) );
		InputJump();
	}

	void UpdateHeadroom()
	{
		var tr = TraceBody( WorldPosition, WorldPosition + Vector3.Up * 100, 0.75f );
		Headroom = tr.Distance;
	}

	bool _wasFalling = false;
	float fallDistance = 0;
	Vector3 fallVelocity = 0;

	void UpdateFalling()
	{
		if ( !Mode.AllowFalling )
		{
			_wasFalling = false;
			fallDistance = 0;
			fallVelocity = default;
			return;
		}

		if ( IsOnGround )
		{
			if ( _wasFalling )
			{
				IEvents.PostToGameObject( GameObject, x => x.OnLanded( fallDistance, fallVelocity ) );

				// play land sounds
				if ( EnableFootstepSounds )
				{
					var volume = fallVelocity.Length.Remap( 50, 800, 0.5f, 5 );
					var vel = fallVelocity.Length;

					PlayFootstepSound( WorldPosition, volume, 0 );
					PlayFootstepSound( WorldPosition, volume, 1 );
				}
			}

			_wasFalling = false;
			fallDistance = 0;
			return;
		}

		_wasFalling = true;
		fallVelocity = Velocity;
		fallDistance += fallVelocity.z * -1 * Time.Delta;

		if ( fallDistance < 0 )
			fallDistance = 0;
	}





	Transform localGroundTransform;
	int groundHash;

	void UpdateGroundEyeRotation()
	{
		if ( GroundObject is null )
		{
			groundHash = default;
			return;
		}

		if ( !RotateWithGround )
		{
			groundHash = default;
			return;
		}

		var hash = HashCode.Combine( GroundObject );

		// Get out transform locally to the ground object
		var localTransform = GroundObject.WorldTransform.ToLocal( WorldTransform );

		// Work out the rotation delta chance since last frame
		var delta = localTransform.Rotation.Inverse * localGroundTransform.Rotation;

		// we only care about the yaw
		var deltaYaw = delta.Angles().yaw;

		//DebugDrawSystem.Current.Text( WorldPosition, $"{delta.Angles().yaw}" );

		// If we're on the same ground and we've rotated
		if ( hash == groundHash && deltaYaw != 0 )
		{
			// rotate the eye angles
			EyeAngles = EyeAngles.WithYaw( EyeAngles.yaw + deltaYaw );

			// rotate the body to avoid it animating to the new position
			if ( UseAnimatorControls && Renderer.IsValid() )
			{
				Renderer.WorldRotation *= new Angles( 0, deltaYaw, 0 );
			}
		}

		// Keep for next frame
		groundHash = hash;
		localGroundTransform = localTransform;
	}



}
