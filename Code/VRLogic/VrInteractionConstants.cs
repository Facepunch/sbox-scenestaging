namespace VRLogic;

/// <summary>
/// 跨元件共用的互動常數（與 ModelDoc attachment 命名一致；程式與 Inspector 預設應對齊）。
/// </summary>
public static class VrInteractionConstants
{
	/// <summary>手部 SkinnedModelRenderer 上常用握持 attachment；須與 DCC／ModelDoc 名稱完全一致（含大小寫）。</summary>
	public const string DefaultGripAttachmentName = "weapon_hold";

	/// <summary>Grip 超過此值視為按下抓取（0–1）。</summary>
	public const float DefaultGripPressThreshold = 0.5f;

	/// <summary>Grip 低於此值視為放開（0–1）。</summary>
	public const float DefaultGripReleaseThreshold = 0.2f;
}
