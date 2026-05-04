namespace VRLogic;

/// <summary>
/// 與引擎無關的 VR 互動規則（插槽 id、距離），供 VRSocket 與單元測試共用。
/// </summary>
public static class VRInteractionRules
{
	public static bool SocketAccepts( string socketAcceptId, string itemSocketId )
	{
		if ( string.IsNullOrEmpty( socketAcceptId ) )
			return true;
		return string.Equals( itemSocketId ?? "", socketAcceptId, StringComparison.Ordinal );
	}

	public static bool IsWithinRadius( float ax, float ay, float az, float bx, float by, float bz, float radius )
	{
		var dx = ax - bx;
		var dy = ay - by;
		var dz = az - bz;
		return MathF.Sqrt( dx * dx + dy * dy + dz * dz ) <= radius;
	}
}
