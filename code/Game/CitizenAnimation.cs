using Sandbox;
using System;

public sealed class CitizenAnimation : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property] public AnimatedModelComponent Target { get; set; }

	/// <summary>
	/// Have the player look at this point in the world
	/// </summary>
	public void WithLook( Vector3 lookDirection, float eyesWeight = 1.0f, float headWeight = 1.0f, float bodyWeight = 1.0f )
	{
		Target.SetAnimLookAt( "aim_eyes", lookDirection );
		Target.SetAnimLookAt( "aim_head", lookDirection );
		Target.SetAnimLookAt( "aim_body", lookDirection );

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

		Target.SetAnimParameter( "move_direction", angle );
		Target.SetAnimParameter( "move_speed", Velocity.Length );
		Target.SetAnimParameter( "move_groundspeed", Velocity.WithZ( 0 ).Length );
		Target.SetAnimParameter( "move_y", sideward );
		Target.SetAnimParameter( "move_x", forward );
		Target.SetAnimParameter( "move_z", Velocity.z );
	}

	public void WithWishVelocity( Vector3 Velocity )
	{
		var dir = Velocity;
		var forward = Target.Transform.Rotation.Forward.Dot( dir );
		var sideward = Target.Transform.Rotation.Right.Dot( dir );

		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		Target.SetAnimParameter( "wish_direction", angle );
		Target.SetAnimParameter( "wish_speed", Velocity.Length );
		Target.SetAnimParameter( "wish_groundspeed", Velocity.WithZ( 0 ).Length );
		Target.SetAnimParameter( "wish_y", sideward );
		Target.SetAnimParameter( "wish_x", forward );
		Target.SetAnimParameter( "wish_z", Velocity.z );
	}

	public Rotation AimAngle
	{
		set
		{
			value = Target.Transform.Rotation.Inverse * value;
			var ang = value.Angles();

			Target.SetAnimParameter( "aim_body_pitch", ang.pitch );
			Target.SetAnimParameter( "aim_body_yaw", ang.yaw );
		}
	}

	public float AimEyesWeight
	{
		get => Target.GetAnimParameterFloat( "aim_eyes_weight" );
		set => Target.SetAnimParameter( "aim_eyes_weight", value );
	}

	public float AimHeadWeight
	{
		get => Target.GetAnimParameterFloat( "aim_head_weight" );
		set => Target.SetAnimParameter( "aim_head_weight", value );
	}

	public float AimBodyWeight
	{
		get => Target.GetAnimParameterFloat( "aim_body_weight" );
		set => Target.SetAnimParameter( "aim_headaim_body_weight_weight", value );
	}


	public float FootShuffle
	{
		get => Target.GetAnimParameterFloat( "move_shuffle" );
		set => Target.SetAnimParameter( "move_shuffle", value );
	}

	public float DuckLevel
	{
		get => Target.GetAnimParameterFloat( "duck" );
		set => Target.SetAnimParameter( "duck", value );
	}

	public float VoiceLevel
	{
		get => Target.GetAnimParameterFloat( "voice" );
		set => Target.SetAnimParameter( "voice", value );
	}

	public bool IsSitting
	{
		get => Target.GetAnimParameterBool( "b_sit" );
		set => Target.SetAnimParameter( "b_sit", value );
	}

	public bool IsGrounded
	{
		get => Target.GetAnimParameterBool( "b_grounded" );
		set => Target.SetAnimParameter( "b_grounded", value );
	}

	public bool IsSwimming
	{
		get => Target.GetAnimParameterBool( "b_swim" );
		set => Target.SetAnimParameter( "b_swim", value );
	}

	public bool IsClimbing
	{
		get => Target.GetAnimParameterBool( "b_climbing" );
		set => Target.SetAnimParameter( "b_climbing", value );
	}

	public bool IsNoclipping
	{
		get => Target.GetAnimParameterBool( "b_noclip" );
		set => Target.SetAnimParameter( "b_noclip", value );
	}

	public bool IsWeaponLowered
	{
		get => Target.GetAnimParameterBool( "b_weapon_lower" );
		set => Target.SetAnimParameter( "b_weapon_lower", value );
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
		get => (HoldTypes)Target.GetAnimParameterInt( "holdtype" );
		set => Target.SetAnimParameter( "holdtype", (int)value );
	}

	public enum Hand
	{
		Both,
		Right,
		Left
	}

	public Hand Handedness
	{
		get => (Hand)Target.GetAnimParameterInt( "holdtype_handedness" );
		set => Target.SetAnimParameter( "holdtype_handedness", (int)value );
	}

	public void TriggerJump()
	{
		Target.SetAnimParameter( "b_jump", true );
	}

	public void TriggerDeploy()
	{
		Target.SetAnimParameter( "b_deploy", true );
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
		get => (MoveStyles)Target.GetAnimParameterInt( "move_style" );
		set => Target.SetAnimParameter( "move_style", (int)value );
	}
}
