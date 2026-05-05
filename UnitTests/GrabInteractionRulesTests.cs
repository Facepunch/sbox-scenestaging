using Microsoft.VisualStudio.TestTools.UnitTesting;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class GrabInteractionRulesTests
{
	[TestMethod]
	public void ShouldStartGrab_true_when_grip_high_no_held_and_candidate()
	{
		Assert.IsTrue( GrabInteractionRules.ShouldStartGrab( 0.51f, 0.5f, hasHeldObject: false, hasTouchingCandidate: true ) );
	}

	[TestMethod]
	public void ShouldStartGrab_false_when_already_holding()
	{
		Assert.IsFalse( GrabInteractionRules.ShouldStartGrab( 1f, 0.5f, hasHeldObject: true, hasTouchingCandidate: true ) );
	}

	[TestMethod]
	public void ShouldStartGrab_false_when_no_candidate()
	{
		Assert.IsFalse( GrabInteractionRules.ShouldStartGrab( 1f, 0.5f, hasHeldObject: false, hasTouchingCandidate: false ) );
	}

	[TestMethod]
	public void ShouldReleaseGrab_true_when_grip_low_and_holding()
	{
		Assert.IsTrue( GrabInteractionRules.ShouldReleaseGrab( 0.19f, 0.2f, hasHeldObject: true ) );
	}

	[TestMethod]
	public void ShouldReleaseGrab_false_when_still_holding_firm()
	{
		Assert.IsFalse( GrabInteractionRules.ShouldReleaseGrab( 0.5f, 0.2f, hasHeldObject: true ) );
	}
}
