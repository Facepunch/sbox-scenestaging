using Sandbox;

public sealed class VRMovement : Component
{
    [Property] public float Speed { get; set; } = 100.0f;
    
    // 引用同一個物件上的 CharacterController
    [Property] public CharacterController Controller { get; set; }

    protected override void OnUpdate()
    {
        if ( Controller == null ) return;

        // 1. 取得左手搖桿輸入 (Vector2)
        var input = Input.VR.LeftHand.Joystick.Value;

        // 2. 取得頭盔的方向，讓移動方向跟著玩家視線
        var headRot = Input.VR.Head.Rotation;
        var forward = headRot.Forward.WithZ( 0 ).Normal;
        var right = headRot.Right.WithZ( 0 ).Normal;

        // 3. 計算移動向量
        Vector3 wishVelocity = (forward * input.y) + (right * input.x);
        wishVelocity *= Speed;

        // 4. 套用重力 (簡單實作)
        if ( !Controller.IsOnGround )
        {
            wishVelocity = wishVelocity.WithZ( Controller.Velocity.z - 400.0f * Time.Delta );
        }
        else
        {
            wishVelocity = wishVelocity.WithZ( 0.0f );
        }

        // 5. 呼叫 CharacterController 進行移動
        Controller.Velocity = wishVelocity;
        Controller.Move();
    }
}