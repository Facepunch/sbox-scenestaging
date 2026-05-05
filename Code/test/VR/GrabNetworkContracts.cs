/// <summary>
/// 多人遊戲預留合約（本專案現為單機）：抓取與插槽應具<strong>單一權威</strong>（擁有該道具的連線或伺服器），
/// 放手事件不應由每個客戶端各自執行 <see cref="VRSocket.NotifyGripReleased"/> 全場掃描。
/// 實作多人時請以 RPC／狀態同步取代，並可將 <see cref="GripReleaseNotification.Publish"/> 改為僅伺服器分發或客戶端預測＋回滾。
/// </summary>
public static class GrabNetworkContracts
{
}
