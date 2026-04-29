using Sandbox;

public sealed class ModelViewerViewModelArms : Component
{
	[Property]
	[Group( "Handling" )]
	bool IsTwoHanded { get; set; } = false;
	[Property]
	[Group( "Handling" )]
	bool IsEmpty { get; set; } = false;
	[Property]
	[Group( "Handling" )]
	bool LoweredWeapon { get; set; } = false;

	[Property]
	[Group( "Handling" )]
	bool HolsterWeapon { get; set; } = false;

	/*
	[Property]
	[Group( "Handling" )]
	bool DeployWeapon { get; set; } = false;
	*/

	[Range( 0, 10 )]
	[Property]
	[Group( "Firing" )]
	int FiringMode { get; set; } = 0;

	[Range( 0, 8 )]
	[Property]
	[Group( "Handling" )]
	int HoldType { get; set; } = 0;

	[Range( 0, 1 )]
	[Property]
	[Group( "IronSight" )]
	float IronSightFireScale { get; set; } = 1;

	[Range( 0, 50 )]
	[Property]
	[Group( "Movement" )]
	float InertiaDamping { get; set; } = 20.0f;


	[Property]
	[ToggleGroup( "MovementOverride" )]
	bool MovementOverride { get; set; } = false;

	[Property]
	[Group( "MovementOverride" )]
	bool MovementJump { get; set; } = false;

	[Property]
	[Group( "MovementOverride" )]
	bool MovementGrounded { get; set; } = true;

	[Property]
	[Group( "MovementOverride" )]
	bool MovementSprinting { get; set; } = false;

	[Property]
	[Group( "MovementOverride" )]
	[Range( 0, 100 )]
	float MovementSpeed { get; set; } = 100.0f;

	[Property]
	[Group( "MovementOverride" )]
	[Range( 0, 360 )]
	float MovementAngle { get; set; } = 0.0f;

	[Property]
	[Group( "MovementOverride" )]
	[Range( -2, 2 )]
	float MovementX { get; set; } = 0.0f;

	[Property]
	[Group( "MovementOverride" )]
	[Range( -2, 2 )]
	float MovementY { get; set; } = 0.0f;

	[Property]
	Vector3 PosOffset { get; set; } = Vector3.Zero;

	[Property]
	bool Visible { get; set; } = false;

	//
	[Property]
	[Group( "Don't worry about these" )]
	private ModelViewerPlayerController MainCamera { get; set; }

	[Property]
	[Group( "Don't worry about these" )]
	CameraComponent CameraComp { get; set; }
	//

	//Use these to keep track of the guns movement
	public float YawInertia { get; private set; }
	public float PitchInertia { get; private set; }
	private float lastYaw;
	private float lastPitch;
	//

	protected override void OnStart()
	{
		base.OnStart();

		GameObject.Parent = CameraComp.GameObject;
	}

	protected override void OnUpdate()
	{
		
		if(MainCamera.FirstPerson )
		{
			GameObject.Tags.Remove( "thirdperson" );
		}else
		{
			GameObject.Tags.Add( "thirdperson" );
		}

		Transform.LocalPosition = PosOffset;

		Visible = Input.Pressed( "slot1" ) ? !Visible : Visible;



		var newYaw = CameraComp.WorldRotation.Yaw();
		var newPitch = CameraComp.WorldRotation.Pitch();

		//var yawDelta = CameraComp.WorldRotation.Angles().yaw - lastYaw;
		var yawDelta = Angles.NormalizeAngle( lastYaw - newYaw );
		var pitchDelta = Angles.NormalizeAngle( lastPitch - newPitch );

		YawInertia += yawDelta;
		PitchInertia += pitchDelta;

		var dir = MainCamera.WishVelocity;
		var forward = MainCamera.WorldRotation.Forward.Dot( dir );
		var sideward = MainCamera.WorldRotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		var child = Components.Get<SkinnedModelRenderer>( FindMode.InSelf );
		var anim = child?.GameObject?.Children?[0].Components.Get<SkinnedModelRenderer>( FindMode.InSelf );

		if ( Visible )
		{
			child.Enabled = true;
			anim.Enabled = true;
		}
		else
		{
			child.Enabled = false;
			anim.Enabled = false;
		}
		if ( !MovementOverride )
		{
			//Movement
			anim.Set( "b_jump", !MainCamera.AnimationHelper.IsGrounded);
			anim.Set( "move_direction", angle );
			anim.Set( "move_speed", MainCamera.WishVelocity.Length );
			anim.Set( "move_groundspeed", MainCamera.WishVelocity.WithZ( 0 ).Length );
			anim.Set( "move_y", sideward );
			anim.Set( "move_x", forward );
			anim.Set( "move_z", MainCamera.WishVelocity.z );
			anim.Set( "b_grounded", MainCamera.AnimationHelper.IsGrounded );
			anim.Set( "move_sprint", Input.Down( "run" ) );
			anim.Set( "b_sprint", Input.Down( "run" ) );
			anim.Set( "b_crouch", Input.Down( "duck" ) );
		}
		else
		{
			anim.Set( "b_jump", MovementJump );
			anim.Set( "move_direction", MovementAngle );
			anim.Set( "move_speed", MovementSpeed );
			anim.Set( "move_groundspeed", MovementSpeed );
			anim.Set( "move_y", MovementX );
			anim.Set( "move_x", MovementY );
			anim.Set( "move_z", 0 );
			anim.Set( "b_grounded", MovementGrounded );
			anim.Set( "move_sprint", MovementSprinting );
			anim.Set( "b_sprint", MovementSprinting );
		}



		//Fire
		anim.Set( "b_attack", Input.Pressed( "Attack1" ) );
		anim.Set( "b_attack_dry", Input.Pressed( "Attack1" ) && IsEmpty );

		//Reload
		anim.Set( "b_reload", Input.Pressed( "Reload" ) );

		//Empty
		anim.Set( "b_empty", IsEmpty );

		//TwoHanded
		anim.Set( "b_twohanded", IsTwoHanded );

		//Iron sight
		anim.Set( "ironsights", Input.Down( "attack2" ) ? 2 : 0 );
		anim.Set( "ironsights_fire_scale", IronSightFireScale );

		//Fire Mode
		anim.Set( "firing_mode", FiringMode );

		//HoldType
		anim.Set( "holdtype_pose", HoldType );

		//Lowered
		anim.Set( "b_lower_weapon", LoweredWeapon );

		//Holster
		anim.Set( "b_holster", HolsterWeapon );

		//Deploy
		//anim.Set( "b_deploy", !HolsterWeapon );

		//Inertia sway
		anim.Set( "aim_yaw_inertia", YawInertia );
		anim.Set( "aim_pitch_inertia", PitchInertia );

		lastYaw = newYaw;
		lastPitch = newPitch;

		YawInertia = YawInertia.LerpTo( 0, Time.Delta * InertiaDamping );
		PitchInertia = PitchInertia.LerpTo( 0, Time.Delta * InertiaDamping );
	}
}
