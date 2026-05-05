using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class LocomotionWishRulesTests
{
	[TestMethod]
	public void ComputePlanarWish_forward_stick_yields_forward_times_speed()
	{
		var f = Vector3.Forward;
		var r = Vector3.Right;
		var w = LocomotionWishRules.ComputePlanarWishFromHeadAxes( f, r, new Vector2( 0, 1 ), 100f );
		Assert.IsTrue( (w - Vector3.Forward * 100f).Length < 0.01f );
	}

	[TestMethod]
	public void ComputePlanarWish_zero_stick_yields_zero()
	{
		var w = LocomotionWishRules.ComputePlanarWishFromHeadAxes( Vector3.Forward, Vector3.Right, default, 100f );
		Assert.IsTrue( w.IsNearlyZero() );
	}
}
