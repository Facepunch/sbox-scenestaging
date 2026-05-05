using Sandbox;

namespace VRLogic;

/// <summary>
/// 以頭部／身體水平前與右向量，將 2D 搖桿轉成水平 wish 方向（Z 由控制器處理）。
/// </summary>
public static class LocomotionWishRules
{
	/// <param name="headForward">已抹平或將抹平 Z 的頭部前向（世界空間）。</param>
	/// <param name="headRight">已抹平或將抹平 Z 的頭部右向（世界空間）。</param>
	/// <param name="stick">.x 左右、.y 前後（與 Input 搖桿約定一致）。</param>
	/// <param name="moveSpeed">水平最大速度標量。</param>
	public static Vector3 ComputePlanarWishFromHeadAxes( Vector3 headForward, Vector3 headRight, Vector2 stick, float moveSpeed )
	{
		var forward = headForward.WithZ( 0 );
		var right = headRight.WithZ( 0 );
		if ( forward.IsNearlyZero() )
			forward = Vector3.Forward;
		else
			forward = forward.Normal;

		if ( right.IsNearlyZero() )
			right = Vector3.Right;
		else
			right = right.Normal;

		var wish = (forward * stick.y) + (right * stick.x);
		return wish * moveSpeed;
	}
}
