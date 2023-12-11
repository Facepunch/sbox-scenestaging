using Sandbox;
using System;

public sealed class CitizenAnimation : Component, Component.ExecuteInEditor
{
	[Property] public SkinnedModelRenderer Target { get; set; }

	[Property] public GameObject EyeSource { get; set; }

	[Property] public GameObject LookAtObject { get; set; }

	[Property, Range( 0.5f, 1.5f)] public float Height { get; set; } = 1.0f;


	[Property] public GameObject IkLeftHand { get; set; }
	[Property] public GameObject IkRightHand { get; set; }
	[Property] public GameObject IkLeftFoot { get; set; }
	[Property] public GameObject IkRightFoot { get; set; }

	protected override void OnUpdate()
	{
		if ( LookAtObject.IsValid() )
		{
			var eyePos = GetEyeWorldTransform.Position;

			var dir = (LookAtObject.Transform.Position - eyePos).Normal;
			WithLook( dir, 1, 0.5f, 0.1f );
		}

		Target.Set( "scale_height", Height );

		// SetIk( "left_hand", ... );
		// SetIk( "right_hand", ... );

		if ( IkLeftHand.IsValid() && IkLeftHand.Active ) SetIk( "hand_left", IkLeftHand.Transform.World );
		else ClearIk( "hand_left" );

		if ( IkRightHand.IsValid() && IkRightHand.Active ) SetIk( "hand_right", IkRightHand.Transform.World );
		else ClearIk( "hand_right" );

		if ( IkLeftFoot.IsValid() && IkLeftFoot.Active ) SetIk( "foot_left", IkLeftFoot.Transform.World );
		else ClearIk( "foot_left" );

		if ( IkRightFoot.IsValid() && IkRightFoot.Active ) SetIk( "foot_right", IkRightFoot.Transform.World );
		else ClearIk( "foot_right" );
	}

	public void SetIk( string name, Transform tx )
	{
		// convert local to model
		tx = Target.Transform.World.ToLocal( tx );

		Target.Set( $"ik.{name}.enabled", true );
		Target.Set( $"ik.{name}.position", tx.Position );
		Target.Set( $"ik.{name}.rotation", tx.Rotation );
	}

	public void ClearIk( string name )
	{
		Target.Set( $"ik.{name}.enabled", false );
	}

	public Transform GetEyeWorldTransform
	{
		get 
		{
			if ( EyeSource.IsValid() ) return EyeSource.Transform.World;

			return Transform.World;
		}
	}


	/// <summary>
	/// Have the player look at this point in the world
	/// </summary>
	public void WithLook( Vector3 lookDirection, float eyesWeight = 1.0f, float headWeight = 1.0f, float bodyWeight = 1.0f )
	{
		Target.SetLookDirection( "aim_eyes", lookDirection );
		Target.SetLookDirection( "aim_head", lookDirection );
		Target.SetLookDirection( "aim_body", lookDirection );

		AimEyesWeight = eyesWeight;
		AimHeadWeight = headWeight;
		AimBodyWeight = bodyWeight;
	}

	public void WithVelocity( Vector3 Velocity )
	{
		var dir = Velocity;
		var forward = Target.Transform.Rotation.Forward.Dot( dir );
		var sideward = Target.Transform.Rotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Target.Set( "move_direction", angle );
		Target.Set( "move_speed", Velocity.Length );
		Target.Set( "move_groundspeed", Velocity.WithZ( 0 ).Length );
		Target.Set( "move_y", sideward );
		Target.Set( "move_x", forward );
		Target.Set( "move_z", Velocity.z );
	}

	public void WithWishVelocity( Vector3 Velocity )
	{
		var dir = Velocity;
		var forward = Target.Transform.Rotation.Forward.Dot( dir );
		var sideward = Target.Transform.Rotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Target.Set( "wish_direction", angle );
		Target.Set( "wish_speed", Velocity.Length );
		Target.Set( "wish_groundspeed", Velocity.WithZ( 0 ).Length );
		Target.Set( "wish_y", sideward );
		Target.Set( "wish_x", forward );
		Target.Set( "wish_z", Velocity.z );
	}

	public Rotation AimAngle
	{
		set
		{
			value = Target.Transform.Rotation.Inverse * value;
			var ang = value.Angles();

			Target.Set( "aim_body_pitch", ang.pitch );
			Target.Set( "aim_body_yaw", ang.yaw );
		}
	}

	public float AimEyesWeight
	{
		get => Target.GetFloat( "aim_eyes_weight" );
		set => Target.Set( "aim_eyes_weight", value );
	}

	public float AimHeadWeight
	{
		get => Target.GetFloat( "aim_head_weight" );
		set => Target.Set( "aim_head_weight", value );
	}

	public float AimBodyWeight
	{
		get => Target.GetFloat( "aim_body_weight" );
		set => Target.Set( "aim_body_weight", value );
	}


	public float FootShuffle
	{
		get => Target.GetFloat( "move_shuffle" );
		set => Target.Set( "move_shuffle", value );
	}

	public float DuckLevel
	{
		get => Target.GetFloat( "duck" );
		set => Target.Set( "duck", value );
	}

	public float VoiceLevel
	{
		get => Target.GetFloat( "voice" );
		set => Target.Set( "voice", value );
	}

	public bool IsSitting
	{
		get => Target.GetBool( "b_sit" );
		set => Target.Set( "b_sit", value );
	}

	public bool IsGrounded
	{
		get => Target.GetBool( "b_grounded" );
		set => Target.Set( "b_grounded", value );
	}

	public bool IsSwimming
	{
		get => Target.GetBool( "b_swim" );
		set => Target.Set( "b_swim", value );
	}

	public bool IsClimbing
	{
		get => Target.GetBool( "b_climbing" );
		set => Target.Set( "b_climbing", value );
	}

	public bool IsNoclipping
	{
		get => Target.GetBool( "b_noclip" );
		set => Target.Set( "b_noclip", value );
	}

	public bool IsWeaponLowered
	{
		get => Target.GetBool( "b_weapon_lower" );
		set => Target.Set( "b_weapon_lower", value );
	}

	public enum HoldTypes
	{
		None,
		Pistol,
		Rifle,
		Shotgun,
		HoldItem,
		Punch,
		Swing,
		RPG
	}

	public HoldTypes HoldType
	{
		get => (HoldTypes)Target.GetInt( "holdtype" );
		set => Target.Set( "holdtype", (int)value );
	}

	public enum Hand
	{
		Both,
		Right,
		Left
	}

	public Hand Handedness
	{
		get => (Hand)Target.GetInt( "holdtype_handedness" );
		set => Target.Set( "holdtype_handedness", (int)value );
	}

	public void TriggerJump()
	{
		Target.Set( "b_jump", true );
	}

	public void TriggerDeploy()
	{
		Target.Set( "b_deploy", true );
	}

	public enum MoveStyles
	{
		Auto,
		Walk,
		Run
	}

	/// <summary>
	/// We can force the model to walk or run, or let it decide based on the speed.
	/// </summary>
	public MoveStyles MoveStyle
	{
		get => (MoveStyles)Target.GetInt( "move_style" );
		set => Target.Set( "move_style", (int)value );
	}
}
