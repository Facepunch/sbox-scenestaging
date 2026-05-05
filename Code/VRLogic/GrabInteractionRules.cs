namespace VRLogic;

/// <summary>
/// 抓取狀態機之純規則（不依賴 Scene／Component），供 VRGrabber 與單元測試共用。
/// </summary>
public static class GrabInteractionRules
{
	public static bool ShouldStartGrab( float gripValue, float pressThreshold, bool hasHeldObject, bool hasTouchingCandidate )
	{
		return gripValue > pressThreshold && !hasHeldObject && hasTouchingCandidate;
	}

	public static bool ShouldReleaseGrab( float gripValue, float releaseThreshold, bool hasHeldObject )
	{
		return gripValue < releaseThreshold && hasHeldObject;
	}
}
