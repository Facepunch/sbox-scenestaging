using Sandbox.Citizen;

//
// This all exists to test the PhysicsCharacterController 
// It needs a clean up !
//

public class PhysicalPlayerController : Component, Component.ICollisionListener
{
	[RequireComponent] public PhysicalCharacterController Controller { get; set; }

	public Vector3 WishVelocity { get; private set; }

	[Property] public GameObject Body { get; set; }
	[Property] public GameObject Eye { get; set; }
	[Property] public CitizenAnimationHelper AnimationHelper { get; set; }
	[Property] public bool FirstPerson { get; set; }
	[Property] public GameObject Gibs { get; set; }

	[Sync] public Angles EyeAngles { get; set; }
	[Sync] public bool IsRunning { get; set; }
	[Sync] public bool IsDucked { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam.IsValid() )
		{
			var ee = cam.WorldRotation.Angles();
			ee.roll = 0;
			EyeAngles = ee;
		}
	}

	protected override void OnUpdate()
	{
		// Eye input
		if ( !IsProxy )
		{
			var ee = EyeAngles;
			ee += Input.AnalogLook * 0.5f;
			ee.roll = 0;
			EyeAngles = ee;

			var cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();

			var lookDir = EyeAngles.ToRotation();

			if ( FirstPerson )
			{
				cam.WorldPosition = Eye.WorldPosition;
				cam.WorldRotation = lookDir;

				foreach ( var c in Body.GetComponentsInChildren<ModelRenderer>() )
				{
					c.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
				}

			}
			else
			{
				cam.WorldPosition = WorldPosition + lookDir.Backward * 300 + Vector3.Up * 75.0f;
				cam.WorldRotation = lookDir;

				foreach ( var c in Body.GetComponentsInChildren<ModelRenderer>() )
				{
					c.RenderType = ModelRenderer.ShadowRenderType.On;
				}

			}

			IsRunning = Input.Down( "Run" );
		}

		if ( !Controller.IsValid() ) return;

		var eee = EyeAngles;
		eee.yaw += Controller.GroundYaw;
		EyeAngles = eee;

		float moveRotationSpeed = 0;

		// rotate body to look angles
		if ( Body.IsValid() )
		{
			if ( Controller.IsClimbing )
			{
				Body.WorldRotation = Rotation.Lerp( Body.WorldRotation, Controller.ClimbingRotation, Time.Delta * 5.0f );
			}
			else
			{
				var targetAngle = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();

				var v = Controller.Velocity.WithZ( 0 );

				if ( v.Length > 10.0f )
				{
					targetAngle = Rotation.LookAt( v, Vector3.Up );
				}

				float rotateDifference = Body.WorldRotation.Distance( targetAngle );

				if ( rotateDifference > 50.0f || Controller.Velocity.Length > 10.0f )
				{
					var newRotation = Rotation.Lerp( Body.WorldRotation, targetAngle, Time.Delta * 2.0f );

					// We won't end up actually moving to the targetAngle, so calculate how much we're actually moving
					var angleDiff = Body.WorldRotation.Angles() - newRotation.Angles(); // Rotation.Distance is unsigned
					moveRotationSpeed = angleDiff.yaw / Time.Delta;

					Body.WorldRotation = newRotation;
				}
			}
		}


		if ( AnimationHelper.IsValid() )
		{
			AnimationHelper.WithVelocity( Controller.WishVelocity );
			AnimationHelper.WithWishVelocity( Controller.WishVelocity );
			AnimationHelper.IsGrounded = Controller.IsOnGround || Controller.IsClimbing;
			AnimationHelper.MoveRotationSpeed = moveRotationSpeed;
			AnimationHelper.WithLook( EyeAngles.Forward, 1, 1, 1.0f );
			AnimationHelper.MoveStyle = IsRunning ? CitizenAnimationHelper.MoveStyles.Run : CitizenAnimationHelper.MoveStyles.Walk;
			AnimationHelper.DuckLevel = IsDucked ? 1 : 0;
			AnimationHelper.IsSwimming = Controller.CurrentMoveMode == Controller.Swimming;
			AnimationHelper.IsClimbing = Controller.CurrentMoveMode == Controller.Climb;

			var skidding = 0.0f;

			if ( Controller.WishVelocity.IsNearZeroLength )
				skidding = Controller.Velocity.Length.Remap( 0, 1000, 0, 1 );

			AnimationHelper.Target.Set( "skid", skidding );

		}
	}

	[Broadcast]
	public void OnJump()
	{
		AnimationHelper?.TriggerJump();
	}

	TimeSince timeSinceJump;

	protected override void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		// create WishVelocity
		{
			var rot = EyeAngles.ToRotation();

			WishVelocity = rot * Input.AnalogMove;

			if ( Controller.IsSwimming && Input.Down( "jump" ) ) WishVelocity += Vector3.Up;

			if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

			if ( Controller.IsClimbing )
			{
				WishVelocity = new Vector3( 0, 0, Input.AnalogMove.x );

				WishVelocity *= 340.0f;

				if ( Input.Down( "jump" ) )
				{
					// Jump away from ladder
					Controller.Jump( Controller.ClimbingRotation.Backward * 200 );
				}
			}
			else
			{
				if ( Input.Down( "Run" ) ) WishVelocity *= 320.0f;
				else WishVelocity *= 110.0f;
			}
		}

		if ( Controller.TimeSinceGrounded < 0.3f && Input.Pressed( "Jump" ) && timeSinceJump > 0.5f )
		{
			timeSinceJump = 0;

			Controller.Jump( Vector3.Up * 300 );
			OnJump();
		}

		if ( Input.Pressed( "score" ) ) FirstPerson = !FirstPerson;

		IsDucked = Input.Down( "duck" );

		if ( Controller.WaterLevel > 0 )
			DebugDrawSystem.Current.Text( WorldPosition + Vector3.Up * 80, $"WaterLevel: {Controller.WaterLevel}" );

		if ( Controller.IsSwimming )
		{
			Controller.WishVelocity = WishVelocity;
		}
		else if ( Controller.IsClimbing )
		{
			Controller.WishVelocity = WishVelocity;
		}
		else
		{

			if ( IsDucked )
			{
				Controller.BodyHeight = 40;
			}
			else
			{
				Controller.BodyHeight = 64;
			}

			Controller.WishVelocity = WishVelocity.WithZ( 0 );
		}



		UpdatePressure();
	}

	public void Explode()
	{
		Gibs.SetParent( Scene, true );
		Gibs.Enabled = true;

		foreach ( var rb in Gibs.GetComponentsInChildren<Rigidbody>() )
		{
			rb.Velocity = Vector3.Random * 1000;
		}

		GameObject.Destroy();
	}


	float nominalPressure => 350;
	float pressure;
	int i = 0;

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		//if ( float.IsNaN( collision.Contact.Impulse ) ) return;

		//var imp = collision.Contact.Impulse / collision.Self.Body.Mass;

		//if ( imp < nominalPressure ) return;
		//if ( collision.Contact.NormalSpeed < 0 ) return;

		//DebugDrawSystem.Current.AddText( collision.Contact.Point + Vector3.Up * i * 16, $"[{imp:n0}] {collision.Contact.Normal} {collision.Contact.NormalSpeed} {collision.Other.Collider}" );
		//pressure += imp;
		//i++;
	}
	void ICollisionListener.OnCollisionUpdate( Collision collision )
	{
		if ( float.IsNaN( collision.Contact.Impulse ) ) return;

		var imp = collision.Contact.Impulse / collision.Self.Body.Mass;

		if ( imp < nominalPressure ) return;

		//DebugDrawSystem.Current.AddText( collision.Contact.Point + Vector3.Up * i * 16, $"[{imp:n0}] {collision.Other.Collider}" );
		//DebugDrawSystem.Current.AddText( collision.Contact.Point, $"{collision.Contact.Impulse}" ).WithTime( 10.0f );

		pressure += imp;
		i++;
	}

	void UpdatePressure()
	{
		i = 0;

		//if ( pressure > 100000 )
		//DebugDrawSystem.Current.AddText( WorldPosition + Vector3.Up * 80, $"pressure: {pressure}\nvelocity: {Controller.Body.Velocity}" );

		if ( pressure > 1000 )
		{
			Log.Info( $"Explode pressure: {pressure}" );
			Explode();
		}

		pressure -= 1000;
		if ( pressure < 0 ) pressure = 0;
	}
}
