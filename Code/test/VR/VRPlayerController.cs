using Sandbox;

public sealed class VRPlayerController : Component
{
    [Property, Group("Components")] 
    public CharacterController Controller { get; set; }

    [Property, Group("Movement")] 
    public float MoveSpeed { get; set; } = 100.0f;
    
    [Property, Group("Movement")] 
    public float TurnSpeed { get; set; } = 120.0f; // 每秒旋轉的角度

    [Property, Group("Movement")]
    public bool UseSnapTurn { get; set; } = false;

    [Property, Group("Movement")]
    public float SnapTurnAngle { get; set; } = 45.0f;

    [Property, Group("Movement")]
    public float SnapTurnThreshold { get; set; } = 0.5f;

    [Property, Group("Movement")]
    public float SnapTurnResetThreshold { get; set; } = 0.2f;

    private bool canSnapTurn = true;

    protected override void OnUpdate()
    {
        if ( Controller == null ) return;

        // ==========================================
        // 1. 右手搖桿：控制視角轉向 (Rotation)
        // ==========================================
        var rightJoystick = Input.VR.RightHand.Joystick.Value;
        
        if ( UseSnapTurn )
        {
            if ( MathF.Abs( rightJoystick.x ) > SnapTurnThreshold && canSnapTurn )
            {
                float turnAmount = rightJoystick.x > 0.0f ? -SnapTurnAngle : SnapTurnAngle;
                Transform.Rotation *= Rotation.FromYaw( turnAmount );
                canSnapTurn = false;
            }
            else if ( MathF.Abs( rightJoystick.x ) < SnapTurnResetThreshold )
            {
                canSnapTurn = true;
            }
        }
        else if ( MathF.Abs( rightJoystick.x ) > 0.1f ) // 加上 0.1f 的 Deadzone 防止搖桿飄移
        {
            // 在 S&box 中，Z 軸向上，Yaw 代表左右轉頭
            float turnAmount = rightJoystick.x * TurnSpeed * Time.Delta;
            Transform.Rotation *= Rotation.FromYaw( turnAmount );
        }

        // ==========================================
        // 2. 左手搖桿：控制物理移動 (Translation)
        // ==========================================
        var leftJoystick = Input.VR.LeftHand.Joystick.Value;

        // 取得頭盔目前的旋轉，讓我們可以「朝著眼睛看的方向」前進
        var headRot = Input.VR.Head.Rotation;
        
        // 抹除 Z 軸，避免玩家低頭看地板時，往前推搖桿會往地下鑽
        var forward = headRot.Forward.WithZ( 0 ).Normal;
        var right = headRot.Right.WithZ( 0 ).Normal;

        // 根據搖桿的 X (左右) 和 Y (前後) 結合頭盔方向計算出最終移動向量
        Vector3 wishVelocity = (forward * leftJoystick.y) + (right * leftJoystick.x);
        wishVelocity *= MoveSpeed;

        // 簡單的重力模擬
        if ( !Controller.IsOnGround )
        {
            wishVelocity = wishVelocity.WithZ( Controller.Velocity.z - 400.0f * Time.Delta );
        }
        else
        {
            wishVelocity = wishVelocity.WithZ( 0.0f );
        }

        // 呼叫 CharacterController 進行移動並處理碰撞
        Controller.Velocity = wishVelocity;
        Controller.Move();
    }
}