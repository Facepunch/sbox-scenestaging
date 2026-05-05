using Sandbox;

/// <summary>
/// 放手事件廣播：預設轉發至 <see cref="VRSocket.NotifyGripReleased"/>；測試或多人可替換 <see cref="Publish"/>。
/// </summary>
public static class GripReleaseNotification
{
	/// <summary>由 <see cref="VRGrabber"/> 於釋放時呼叫；多人時可替換為網路匯流排（見 <see cref="GrabNetworkContracts"/>）。</summary>
	public static Action<Scene, GameObject>? Publish { get; set; } = VRSocket.NotifyGripReleased;
}
