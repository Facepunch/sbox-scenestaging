
using Sandbox;
using Sandbox.Citizen;
using static Sandbox.ModelRenderer;
using static Sandbox.PhysicsContact;

public class ModelViewerPlayerController : Component
{
	public enum CameraMode
	{
		Mayamode,
		Character,
		Free
	}

	[Property] public Vector3 Gravity { get; set; } = new Vector3( 0, 0, 800 );
	public Vector3 WishVelocity { get; private set; }
	[Property] public GameObject Body { get; set; }
	[Property] public GameObject Eye { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public bool FirstPerson { get; set; }
	[Property] public CameraMode CurrentCameraMode { get; set; }
	[Property] public float Distance { get; set; } = 200;
	public Angles CameraAngles { get; set; }
	bool IsDucking { get; set; }
	float ducklevel = 0;
	bool IsAlt { get; set; }

	public Angles EyeAngles;

	[Sync]
	public bool IsRunning { get; set; }

	public Angles TargetAngles { get; set; }
	public Vector3 MayaPosOffet { get; set; } = Vector3.Up * 64;
	private Vector2 cameraAngles;

	public float SkidLevel { get; set; } = 0;

	Angles MayaAngles { get; set; }
	float LerpMode = 0;

	float startingFOV;

	bool IsFlying;
	public bool IsSwiming = false;

	bool usingGravity = true;

	public bool IsHoldingObject;

	protected override void OnAwake()
	{
		base.OnAwake();

	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam is not null )
		{
			EyeAngles = cam.WorldRotation.Angles();
			EyeAngles.roll = 0;

			startingFOV = cam.FieldOfView;
		}
	}

	public float CurrentGroundAngle
	{
		get
		{
			var trace = Scene.Trace.Ray( WorldPosition + Vector3.Up * 2, WorldPosition + Vector3.Down * 6 )
			.WithoutTags( "player", "collider" )
			.Radius( 8 )
			.Run();

			//	Gizmo.Draw.Color = Color.Red;
			//	Gizmo.Draw.Line( Target.WorldPosition + Vector3.Up * 2, Target.WorldPosition + Vector3.Down * 6 );

			if ( !trace.Hit )
				return 0;

			return trace.Normal.Angle( Vector3.Up );
		}
	}

	protected override void OnUpdate()
	{
		// Eye input
		if ( !IsProxy )
		{
			if (CurrentCameraMode == CameraMode.Character && !IsHoldingObject )
			{
		
					EyeAngles.pitch += Input.MouseDelta.y * 0.1f;
					EyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
					EyeAngles.roll = 0;

					EyeAngles.pitch = Math.Clamp( EyeAngles.pitch, -89.9f, 89.9f );
				
			}
			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
			var lookDir = EyeAngles.ToRotation();
			

			if ( CurrentCameraMode == CameraMode.Character )
			{
				if ( FirstPerson )
				{
					cam.WorldPosition = Eye.WorldPosition;
					cam.WorldRotation = lookDir;					
				}
				else
				{
					cam.WorldPosition = WorldPosition + lookDir.Backward * Distance + Vector3.Up * (IsDucking ? 48 : 64);
					cam.WorldRotation = lookDir;
				}
			}
			else if ( CurrentCameraMode == CameraMode.Mayamode )
			{
				var newCameraPosition = WorldPosition + MayaPosOffet + cam.WorldRotation.Backward * Distance;

				cam.WorldPosition = newCameraPosition;
				cam.WorldRotation = Rotation.From( cameraAngles.x, -cameraAngles.y, 0 );

				EyeAngles.pitch += Input.MouseDelta.y * 0.1f;
				EyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
				EyeAngles.roll = 0;

				EyeAngles.pitch = Math.Clamp( EyeAngles.pitch, -89.9f, 89.9f );

			}
			else if(CurrentCameraMode == CameraMode.Free)
			{
				HandleFlyCameraMovement( cam.GameObject );
			}

			if(IsDucking)
			{
				Eye.WorldPosition = Eye.WorldPosition.LerpTo( WorldPosition + Vector3.Up * 32, Time.Delta * 10 );
			}
			else
			{
				Eye.WorldPosition = Eye.WorldPosition.LerpTo( WorldPosition + Vector3.Up * 64, Time.Delta * 10 );
			}

			IsRunning = Input.Down( "Run" );

			IsAlt = Input.Down( "Walk" );

			float x = Input.MouseDelta.x;
			float y = Input.MouseDelta.y;

			if ( CurrentCameraMode == CameraMode.Mayamode )
			{
				if ( IsAlt )
				{
					if ( Input.Down( "attack1" ) )
					{
						Distance += Input.MouseDelta.y;
					}
					if ( Input.Down( "attack2" ) )
					{

						var translateX = cam.WorldRotation.Right * (-x * 10 * RealTime.Delta);
						var translateY = cam.WorldRotation.Up * (y * 10 * RealTime.Delta);

						// Move the camera in the local coordinates
						MayaPosOffet += translateX;
						MayaPosOffet += translateY;

						//MayaPosOffet += Input.MouseDelta.x * 0.1f * MayaRotation.Right;
						//MayaPosOffet += Input.MouseDelta.y * 0.1f * MayaRotation.Up;
					}
					else if ( !Input.Down( "attack1" ) )
					{
						cameraAngles += new Vector2( y * 0.10f, x * 0.10f );
						cameraAngles.x = Math.Clamp( cameraAngles.x, -89.9f, 89.9f );
						//MayaRotation = cameraAngles;
					}
				}

				//cam.WorldPosition = WorldPosition + MayaRotation.Backward * Distance + Vector3.Up * 75.0f;
			}
		}

		CurrentCameraMode = Input.Pressed( "menu" ) ? CurrentCameraMode == CameraMode.Character ? CameraMode.Mayamode : CameraMode.Character : CurrentCameraMode;

		IsFlying = Input.Pressed( "Fly" ) ? !IsFlying : IsFlying;

		if (Input.Pressed("flashlight"))
		{
			CurrentCameraMode = CameraMode.Free;
			lastPos = Eye.WorldPosition;
		}
		
		if(CurrentCameraMode == CameraMode.Free)
		{
			if ( Input.Down( "slot1" ) ) LerpMode = 0.0f;
			if ( Input.Down( "slot2" ) ) LerpMode = 0.5f;
			if ( Input.Down( "slot3" ) ) LerpMode = 0.9f;
		}
		
		IsDucking = Input.Down( "duck" ) && CurrentCameraMode != CameraMode.Free;

		var cc = GameObject.Components.Get<ModelViewerCharacterController>();
		if ( cc is null ) return;

		FirstPerson = Input.Pressed( "view" ) ? !FirstPerson : FirstPerson;

		if ( FirstPerson && CurrentCameraMode != CameraMode.Free )
		{
			var citi = Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants );

			foreach ( var c in citi )
			{
				if ( c.GameObject.Tags.Has( "arms" ) )
					continue;

				if ( string.Equals( c.GameObject.Name, "arms", System.StringComparison.InvariantCultureIgnoreCase ) )
					continue;

				c.RenderType = ShadowRenderType.ShadowsOnly;

			}
		}
		else
		{
			var citi = Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants );

			foreach ( var c in citi )
			{
				c.RenderType = ShadowRenderType.On;
			}
		}

		float rotateDifference = 0;

		if ( !IsAlt  )
		{
			TargetAngles = Rotation.LookAt( EyeAngles.Forward ).Angles().WithPitch( 0 ).WithRoll( 0 );

		}
		// rotate body to look angles
		if ( Body is not null && CurrentCameraMode != CameraMode.Free )
		{
			//if ( WishVelocity.Length > 0 && cc.IsOnGround )
			//{
			//	TargetAngles = Rotation.LookAt( WishVelocity ).Angles();
			//}
			Body.WorldRotation = Rotation.Slerp( Body.WorldRotation, Rotation.From( TargetAngles ), 8f * Time.Delta );
		}

		SkidLevel = SkidLevel.LerpTo( CurrentGroundAngle >= cc.GroundAngle && !cc.IsOnGround ? 1 : 0 , Time.Delta * 5f );

		if ( AnimationHelper is not null )
		{
			AnimationHelper.WithVelocity( cc.Velocity );
			AnimationHelper.WithWishVelocity( WishVelocity );
			AnimationHelper.IsGrounded = cc.IsOnGround;
			AnimationHelper.IsNoclipping = IsFlying;
			AnimationHelper.IsSwimming = IsSwiming;
			AnimationHelper.Target.Set( "skid", SkidLevel );

			rotateDifference = Input.MouseDelta.x * 10;
			AnimationHelper.MoveRotationSpeed = rotateDifference;

			if ( AnimationHelper.Target.GetFloat("skid") > 0.1f )
			{
				AnimationHelper.IsGrounded = true;
			}

			if ( CurrentCameraMode != CameraMode.Free )
			{
				AnimationHelper.WithLook( EyeAngles.Forward, 1, 1, 1.0f );
			}
			AnimationHelper.MoveStyle = IsRunning ? CitizenAnimationHelper.MoveStyles.Run : CitizenAnimationHelper.MoveStyles.Walk;
			AnimationHelper.DuckLevel = MathX.Lerp( AnimationHelper.DuckLevel, IsDucking ? 1 : 0, Time.Delta * 10.0f );
		}
	}

	[Broadcast]
	public void OnJump( float floatValue, string dataString, object[] objects, Vector3 position )
	{
		AnimationHelper?.TriggerJump();
	}

	float fJumps;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;
		if ( CurrentCameraMode != CameraMode.Free )
		{
			BuildWishVelocity();
		}
		else
		{
			WishVelocity = 0;
		}
		
		var cc = GameObject.Components.Get<ModelViewerCharacterController>();

		if ( cc.IsOnGround && Input.Down( "Jump" ) && CurrentCameraMode != CameraMode.Free )
		{
			float flGroundFactor = 1.0f;
			float flMul = 268.3281572999747f * 1.2f;
			//if ( Duck.IsActive )
			//	flMul *= 0.8f;

			cc.Punch( Vector3.Up * flMul * flGroundFactor );
			//	cc.IsOnGround = false;

			OnJump( fJumps, "Hello", new object[] { Time.Now.ToString(), 43.0f }, Vector3.Random );

			fJumps += 1.0f;

		}

		if(IsFlying)
		{ 
			usingGravity = false; 
		}
		else if (IsSwiming)
		{ 
			usingGravity = false;
		}
		else
		{
			usingGravity = true;
		}

		if ( usingGravity )
		{
			if ( cc.IsOnGround )
			{
				cc.Velocity = cc.Velocity.WithZ( 0 );
				cc.Accelerate( WishVelocity );
				cc.ApplyFriction( 4.0f );
			}
			else
			{
				cc.Velocity -= Gravity * Time.Delta * 0.5f;
				cc.Accelerate( WishVelocity.ClampLength( 50 ) );
				cc.ApplyFriction( 0.1f );
			}

			cc.Move();

			if ( !cc.IsOnGround )
			{
				cc.Velocity -= Gravity * Time.Delta * 0.5f;
			}
			else
			{
				cc.Velocity = cc.Velocity.WithZ( 0 );
			}
		}
		else
		{
			FlyingMovement();
		}
	}

	public void BuildWishVelocity()
	{
		var rot = EyeAngles.ToRotation();

		WishVelocity = 0;

		if ( Input.Down( "Forward" ) ) WishVelocity += rot.Forward;
		if ( Input.Down( "Backward" ) ) WishVelocity += rot.Backward;
		if ( Input.Down( "Left" ) ) WishVelocity += rot.Left;
		if ( Input.Down( "Right" ) ) WishVelocity += rot.Right;

		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

		if ( Input.Down( "Run" ) ) WishVelocity *= 320.0f;
		else if ( Input.Down( "duck" ) ) WishVelocity *= 75f;
		else WishVelocity *= 110.0f;
	}

	Vector3 lastPos;
	float overrideFOV = 75;
	public float flySpeed = 50.0f;
	private void HandleFlyCameraMovement(GameObject CameraObject)
	{
		var direction = Vector3.Zero;

		if ( Input.Down( "Forward" ) )
			direction += CameraObject.WorldRotation.Forward;
		if ( Input.Down( "Backward" ) )
			direction -= CameraObject.WorldRotation.Forward;
		if ( Input.Down( "Left" ) )
			direction -= CameraObject.WorldRotation.Right;
		if ( Input.Down( "Right" ) )
			direction += CameraObject.WorldRotation.Right;
		if ( Input.Down( "Jump" ) ) // Assuming "Space" is for moving upwards
			direction += Vector3.Up;
		if ( Input.Down( "Duck" ) ) // Assuming "LeftShift" is for moving downwards
			direction -= Vector3.Up;

		flySpeed = Input.Down( "Run" ) ? 150.0f : 50.0f;

		// Normalize the direction to ensure consistent speed in diagonal movement
		// Normalize the direction to ensure consistent speed in diagonal movement
		float magnitude = direction.Length;
		if ( magnitude > 0 )
		{
			direction.x /= magnitude;
			direction.y /= magnitude;
			direction.z /= magnitude;
		}

		lastPos = Vector3.Lerp( lastPos, lastPos + direction * flySpeed, Time.Delta * 10.0f );

		// Update the position based on the direction
		CameraObject.WorldPosition = Vector3.Lerp( CameraObject.WorldPosition, lastPos, 10 * RealTime.Delta * (1 - LerpMode) );

		// Adjusting view direction based on mouse movement
		EyeAngles.pitch += Input.MouseDelta.y * 0.03f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.03f;

		// Clamp pitch to prevent over-rotation (optional but useful to prevent full flips)
		EyeAngles.pitch = Math.Clamp( EyeAngles.pitch, -89.9f, 89.9f );

		if ( Input.Down( "Attack2" ) )
		{
			var cam = CameraObject.Components.Get<CameraComponent>();

			overrideFOV += Input.MouseDelta.y * 0.1f * (overrideFOV / 30f);
			overrideFOV = overrideFOV.Clamp( 50, 150 );

			cam.FieldOfView = overrideFOV;
		}
		else
		{
			// Convert EyeAngles to a rotation and set to the camera
			CameraObject.WorldRotation = Rotation.Lerp( CameraObject.WorldRotation, Rotation.From( EyeAngles ), 10 * RealTime.Delta * (1 - LerpMode) );
		}

	}

	private void FlyingMovement()
	{
		var direction = Vector3.Zero;

		var cc = GameObject.Components.Get<ModelViewerCharacterController>();

		var rot = EyeAngles.ToRotation();

		if ( Input.Down( "Forward" ) )
			direction += rot.Forward;
		if ( Input.Down( "Backward" ) )
			direction -= rot.Forward;
		if ( Input.Down( "Left" ) )
			direction -= rot.Right;
		if ( Input.Down( "Right" ) )
			direction += rot.Right;
		if ( Input.Down( "Jump" ) ) // Assuming "Space" is for moving upwards
			direction += Vector3.Up;
		if ( Input.Down( "Duck" ) ) // Assuming "LeftShift" is for moving downwards
			direction -= Vector3.Up;

		flySpeed = Input.Down( "Run" ) ? 500.0f : 300.0f;

		if ( IsSwiming )
		{
			flySpeed = Input.Down( "Run" ) ? 200.0f : 100.0f;
		}

		// Normalize the direction to ensure consistent speed in diagonal movement
		float magnitude = direction.Length;
		if ( magnitude > 0 )
		{
			direction.x /= magnitude;
			direction.y /= magnitude;
			direction.z /= magnitude;
		}

		cc.Velocity = Vector3.Lerp( cc.Velocity, cc.Velocity + direction * flySpeed, Time.Delta * 10.0f );
		cc.Accelerate( direction );
		cc.ApplyFriction( 5f );

		cc.IsOnGround = false;

		cc.Move();
	}
}
