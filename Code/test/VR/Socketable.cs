using Sandbox;

/// <summary>
/// 標記可被 <see cref="VRSocket"/> 收納的物件，並提供插槽對齊用的世界座標參考。
/// </summary>
public sealed class Socketable : Component
{
    [Property, Description( "與 VRSocket.AcceptId 相同時才可插入該槽；留空表示不限制（仍須通過槽端 AcceptId 規則）。" )]
    public string SocketId { get; set; } = "";

    [Property, Description( "可選：對齊插槽時使用的世界座標點（通常為子物件）；留空則用本物件 WorldPosition。" )]
    public GameObject AttachPivot { get; set; }

    /// <summary>目前鎖定的插槽；未插槽時為 null。</summary>
    public VRSocket CurrentSocket { get; private set; }

    internal void NotifySocketed( VRSocket socket )
    {
        CurrentSocket = socket;
    }

    internal void NotifyUnsocketed()
    {
        CurrentSocket = null;
    }

    public Vector3 GetAttachWorldPosition()
    {
        if ( AttachPivot.IsValid() )
            return AttachPivot.WorldPosition;
        return GameObject.WorldPosition;
    }
}
