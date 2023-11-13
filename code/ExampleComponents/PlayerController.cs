using Sandbox;
using System.Drawing;


public class PlayerController : BaseComponent
{
	[Property] public Vector3 Gravity { get; set; } = new Vector3( 0, 0, 800 );

	[Range( 0, 400)]
	[Property] public float CameraDistance { get; set; } = 200.0f;

	public Vector3 WishVelocity { get; private set; }

	[Property] GameObject Body { get; set; }
	[Property] GameObject Eye { get; set; }
	[Property] bool FirstPerson { get; set; }
	[Property] CitizenAnimation AnimationHelper { get; set; }

	public Angles EyeAngles;

	public override void Update()
	{
		// Eye input
		EyeAngles.pitch += Input.MouseDelta.y * 0.1f;
		EyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
		EyeAngles.roll = 0;

		// Update camera position
		var camera = GameObject.GetComponent<CameraComponent>( true, true );
		if ( camera is not null )
		{
			var camPos = Eye.Transform.Position - EyeAngles.ToRotation().Forward * CameraDistance;

			if ( FirstPerson ) camPos = Eye.Transform.Position + EyeAngles.ToRotation().Forward * 8;

			camera.Transform.Position = camPos;
			camera.Transform.Rotation = EyeAngles.ToRotation();
		}

		var cc = GameObject.GetComponent<CharacterController>();
		if ( cc is null ) return;

		float rotateDifference = 0;

		// rotate body to look angles
		if ( Body is not null )
		{
			var targetAngle = new Angles( 0, EyeAngles.yaw, 0 ).ToRotation();

			var v = cc.Velocity.WithZ( 0 );

			if ( v.Length > 10.0f )
			{
				targetAngle = Rotation.LookAt( v, Vector3.Up );
			}

			rotateDifference = Body.Transform.Rotation.Distance( targetAngle );

			if ( rotateDifference > 50.0f || cc.Velocity.Length > 10.0f )
			{
				Body.Transform.Rotation = Rotation.Lerp( Body.Transform.Rotation, targetAngle, Time.Delta * 2.0f );
			}
		}


		if ( AnimationHelper is not null )
		{
			AnimationHelper.WithVelocity( cc.Velocity );
			AnimationHelper.IsGrounded = cc.IsOnGround;
			AnimationHelper.FootShuffle = rotateDifference;
			AnimationHelper.WithLook( EyeAngles.Forward, 1, 1, 1.0f );
			AnimationHelper.MoveStyle = Input.Down( "Run" ) ? CitizenAnimation.MoveStyles.Run : CitizenAnimation.MoveStyles.Walk;
		}
	}

	public override void FixedUpdate()
	{
		BuildWishVelocity();

		var cc = GameObject.GetComponent<CharacterController>();

		if ( cc.IsOnGround && Input.Down( "Jump" ) )
		{
			float flGroundFactor = 1.0f;
			float flMul = 268.3281572999747f * 1.2f;
			//if ( Duck.IsActive )
			//	flMul *= 0.8f;

			cc.Punch( Vector3.Up * flMul * flGroundFactor );
			//	cc.IsOnGround = false;

			AnimationHelper?.TriggerJump();
		}

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
		else WishVelocity *= 70.0f;
	}
}
