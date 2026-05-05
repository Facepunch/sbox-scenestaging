using Sandbox;
using VRLogic;

/// <summary>
/// VR／桌面本體移動：轉向於 <see cref="OnUpdate"/>；位移、跳躍、摩擦與加速於 <see cref="OnFixedUpdate"/> 與雙手物理一致。
/// </summary>
public sealed class VRPlayerController : Component
{
	[Property, Group( "Components" )]
	public CharacterController Controller { get; set; }

	[Property, Group( "Movement" )]
	public float MoveSpeed { get; set; } = 100.0f;

	[Property, Group( "Movement" )]
	public float TurnSpeed { get; set; } = 120.0f;

	[Property, Group( "Movement" )]
	public bool UseSnapTurn { get; set; }

	[Property, Group( "Movement" )]
	public float SnapTurnAngle { get; set; } = 45.0f;

	[Property, Group( "Movement" )]
	public float SnapTurnThreshold { get; set; } = 0.5f;

	[Property, Group( "Movement" )]
	public float SnapTurnResetThreshold { get; set; } = 0.2f;

	[Property, Group( "Movement" ), Description( "關閉時僅保留搖桿位移，不處理右手轉向。" )]
	public bool EnableRightStickTurn { get; set; } = true;

	[Property, Group( "Movement" )]
	public float JumpStrength { get; set; } = 300f;

	[Property, Group( "Movement" )]
	public float GroundFriction { get; set; } = 6f;

	[Property, Group( "Movement" )]
	public float AirFriction { get; set; } = 0.2f;

	[Property, Group( "Movement" )]
	public float AirWishMax { get; set; } = 50f;

	[Property, Group( "Movement" ), Description( "啟用後以 Input.Down(\"duck\") 切換蹲伏高度（與 Walker 風格類似）。" )]
	public bool EnableCrouch { get; set; }

	[Property, Group( "Movement" )]
	public float StandHeight { get; set; } = 64f;

	[Property, Group( "Movement" )]
	public float CrouchHeight { get; set; } = 36f;

	bool canSnapTurn = true;
	RealTimeSince lastJump;
	bool crouching;

	protected override void OnUpdate()
	{
		if ( Controller is null )
			return;

		var inVr = Game.IsRunningInVR;
		Vector2 rightJoystick;
		Vector2 leftJoystick;

		if ( inVr )
		{
			rightJoystick = Input.VR.RightHand.Joystick.Value;
			leftJoystick = Input.VR.LeftHand.Joystick.Value;
		}
		else
		{
			leftJoystick = Input.AnalogMove;
			rightJoystick = default;
		}

		if ( EnableRightStickTurn && UseSnapTurn )
		{
			if ( MathF.Abs( rightJoystick.x ) > SnapTurnThreshold && canSnapTurn )
			{
				float turnAmount = rightJoystick.x > 0.0f ? SnapTurnAngle : -SnapTurnAngle;
				Transform.Rotation *= Rotation.FromYaw( turnAmount );
				canSnapTurn = false;
			}
			else if ( MathF.Abs( rightJoystick.x ) < SnapTurnResetThreshold )
			{
				canSnapTurn = true;
			}
		}
		else if ( EnableRightStickTurn && MathF.Abs( rightJoystick.x ) > 0.1f )
		{
			float turnAmount = -rightJoystick.x * TurnSpeed * Time.Delta;
			Transform.Rotation *= Rotation.FromYaw( turnAmount );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( Controller is null )
			return;

		var inVr = Game.IsRunningInVR;
		Vector2 leftJoystick;
		Rotation headRot;

		if ( inVr )
		{
			leftJoystick = Input.VR.LeftHand.Joystick.Value;
			headRot = Input.VR.Head.Rotation;
		}
		else
		{
			leftJoystick = Input.AnalogMove;
			var cam = Scene.Camera;
			headRot = cam.IsValid() ? cam.WorldTransform.Rotation : Transform.Rotation;
		}

		if ( EnableCrouch )
			UpdateCrouch();

		var forward = headRot.Forward;
		var right = headRot.Right;
		var wish = LocomotionWishRules.ComputePlanarWishFromHeadAxes( forward, right, leftJoystick, MoveSpeed );

		var cc = Controller;
		var halfGravity = Scene.PhysicsWorld.Gravity * Time.Delta * 0.5f;

		if ( cc.IsOnGround && lastJump > 0.3f && Input.Pressed( "jump" ) )
		{
			lastJump = 0;
			cc.Punch( Vector3.Up * JumpStrength );
		}

		wish = wish.WithZ( 0 );

		cc.ApplyFriction( cc.IsOnGround ? GroundFriction : AirFriction );

		if ( cc.IsOnGround )
		{
			cc.Velocity = cc.Velocity.WithZ( 0 );
			cc.Accelerate( wish );
		}
		else
		{
			cc.Velocity += halfGravity;
			cc.Accelerate( wish.ClampLength( AirWishMax ) );
		}

		cc.Move();

		if ( !cc.IsOnGround )
			cc.Velocity += halfGravity;
		else
			cc.Velocity = cc.Velocity.WithZ( 0 );
	}

	void UpdateCrouch()
	{
		var wishDuck = Input.Down( "duck" );
		if ( wishDuck == crouching )
			return;

		if ( wishDuck )
		{
			Controller.Height = CrouchHeight;
			crouching = true;
			if ( !Controller.IsOnGround )
			{
				var duckDelta = StandHeight - CrouchHeight;
				Controller.MoveTo( Transform.Position + Vector3.Up * duckDelta, false );
				Transform.ClearLerp();
			}
			return;
		}

		if ( !wishDuck && crouching )
		{
			var tr = Controller.TraceDirection( Vector3.Up * (StandHeight - CrouchHeight) );
			if ( tr.Hit )
				return;

			Controller.Height = StandHeight;
			crouching = false;
		}
	}
}
