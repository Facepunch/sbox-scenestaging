using Sandbox;

public sealed class VRFallbackSimulator : Component
{
    [Property, Group("References")] public GameObject CameraRig { get; set; }
    [Property, Group("References")] public GameObject LeftHand { get; set; }
    [Property, Group("References")] public GameObject RightHand { get; set; }

    [Property, Group("Settings")] public float ReachDistance { get; set; } = 30f; // 手距離眼睛多遠

    protected override void OnUpdate()
    {
        // 1. 如果偵測到玩家有戴真實的 VR 頭盔，這個模擬器就直接關閉不做事
        if ( Game.IsRunningInVR ) return;

        // 2. 模擬頭盔轉動 (變成一般的 FPS 鍵鼠視角)
        if ( CameraRig != null )
        {
            var angles = CameraRig.Transform.LocalRotation.Angles();
            angles += Input.AnalogLook; // 讀取滑鼠移動
            angles.pitch = angles.pitch.Clamp( -89f, 89f ); // 限制上下看角度
            CameraRig.Transform.LocalRotation = angles.ToRotation();
            
            // 讓玩家身體的 Root 跟著左右轉
            Transform.Rotation = Rotation.FromYaw( angles.yaw );
        }

        // 3. 模擬雙手位置 (把手掛在攝影機前方)
        var camPos = CameraRig.Transform.Position;
        var camRot = CameraRig.Transform.Rotation;

        if ( LeftHand != null )
        {
            // 將左手放在攝影機前方 + 偏左 + 偏下
            LeftHand.Transform.Position = camPos + camRot.Forward * ReachDistance + camRot.Left * 10f - camRot.Up * 10f;
            LeftHand.Transform.Rotation = camRot;
        }

        if ( RightHand != null )
        {
            // 將右手放在攝影機前方 + 偏右 + 偏下
            RightHand.Transform.Position = camPos + camRot.Forward * ReachDistance + camRot.Right * 10f - camRot.Up * 10f;
            RightHand.Transform.Rotation = camRot;
        }
    }
}