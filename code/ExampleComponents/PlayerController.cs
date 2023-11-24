using Sandbox;

[Category("Player")]
public class PlayerController : BaseComponent
{
	private Angles _eyeAngles;
	private Vector3 _wishVelocity;

	private const float FlGroundFactor = 1.0f;
	private const float FlMul = 268.3281572999747f * 1.2f;
	
	[Property] public GameObject Eye { get; set; }
	[Property] public bool FirstPerson { get; set; } = false;
	[Property] public float CameraDistance { get; set; } = 200.0f;
	[Property] public Vector3 Gravity { get; set; } = new( 0, 0, 800 );
	[Property] public GameObject Body { get; set; }
	[Property] public CitizenAnimation AnimationHelper { get; set; }

	public override void Update()
	{
		CalculateEyesAngles();
		SetCameraPosition();
		
		var cc = GameObject.GetComponent<CharacterController>();
		if ( cc is null ) return;

		var rotateDifference = 0f;

		// rotate body to look angles
		if ( Body is not null )
		{
			var targetAngle = new Angles( 0, _eyeAngles.yaw, 0 ).ToRotation();

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
			AnimationHelper.WithLook( _eyeAngles.Forward, 1, 1, 1.0f );
			AnimationHelper.MoveStyle = Input.Down( "Run" ) ? CitizenAnimation.MoveStyles.Run : CitizenAnimation.MoveStyles.Walk;
		}
	}

	public override void FixedUpdate()
	{
		BuildWishVelocity();

		var cc = GameObject.GetComponent<CharacterController>();

		if ( cc.IsOnGround && Input.Down( "Jump" ) )
		{
			//if ( Duck.IsActive )
			//	flMul *= 0.8f;

			cc.Punch( Vector3.Up * FlMul * FlGroundFactor );
			//	cc.IsOnGround = false;

			AnimationHelper?.TriggerJump();
		}

		if ( cc.IsOnGround )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			cc.Accelerate( _wishVelocity );
			cc.ApplyFriction( 4.0f );
		}
		else
		{
			cc.Velocity -= Gravity * Time.Delta * 0.5f;
			cc.Accelerate( _wishVelocity.ClampLength( 50 ) );
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

	private void CalculateEyesAngles()
	{
		_eyeAngles.pitch += Input.MouseDelta.y * 0.1f;
		_eyeAngles.yaw -= Input.MouseDelta.x * 0.1f;
		_eyeAngles.roll = 0;
	}
    
	private void SetCameraPosition()
	{
		var camera = GetComponent<CameraComponent>(deep: true);
		
		var camPos = Eye.Transform.Position - _eyeAngles.ToRotation().Forward * CameraDistance;
		
		if (FirstPerson)
			camPos = Eye.Transform.Position + _eyeAngles.ToRotation().Forward * 8;

		var hasSpringArm = TryGetComponent<SpringArmComponent>(out var springArm, deep: true);
		
		if (hasSpringArm)
		{
			springArm.Transform.Rotation = _eyeAngles.ToRotation();
			return;
		}

		camera.Transform.Rotation = _eyeAngles.ToRotation();
		camera.Transform.Position = camPos;
	}
	
	private void BuildWishVelocity()
	{
		var rot = _eyeAngles.ToRotation();

		_wishVelocity = 0;

		if ( Input.Down( "Forward" ) ) _wishVelocity += rot.Forward;
		if ( Input.Down( "Backward" ) ) _wishVelocity += rot.Backward;
		if ( Input.Down( "Left" ) ) _wishVelocity += rot.Left;
		if ( Input.Down( "Right" ) ) _wishVelocity += rot.Right;

		_wishVelocity = _wishVelocity.WithZ( 0 );

		if ( !_wishVelocity.IsNearZeroLength ) _wishVelocity = _wishVelocity.Normal;

		if ( Input.Down( "Run" ) ) _wishVelocity *= 320.0f;
		else _wishVelocity *= 70.0f;
	}
}
