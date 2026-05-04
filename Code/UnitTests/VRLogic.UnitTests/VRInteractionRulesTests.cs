using Microsoft.VisualStudio.TestTools.UnitTesting;
using VRLogic;

namespace VRLogic.UnitTests;

[TestClass]
public sealed class VRInteractionRulesTests
{
	[TestMethod]
	public void SocketAccepts_EmptyAccept_accepts_any_item_id()
	{
		Assert.IsTrue( VRInteractionRules.SocketAccepts( "", "magazine" ) );
		Assert.IsTrue( VRInteractionRules.SocketAccepts( null, "magazine" ) );
	}

	[TestMethod]
	public void SocketAccepts_matching_ids_accepts()
	{
		Assert.IsTrue( VRInteractionRules.SocketAccepts( "a", "a" ) );
	}

	[TestMethod]
	public void SocketAccepts_mismatch_rejects()
	{
		Assert.IsFalse( VRInteractionRules.SocketAccepts( "a", "b" ) );
	}

	[TestMethod]
	public void IsWithinRadius_same_point_true()
	{
		Assert.IsTrue( VRInteractionRules.IsWithinRadius( 0, 0, 0, 0, 0, 0, 1f ) );
	}

	[TestMethod]
	public void IsWithinRadius_on_sphere_boundary_true()
	{
		Assert.IsTrue( VRInteractionRules.IsWithinRadius( 0, 0, 0, 3f, 4f, 0f, 5f ) );
	}

	[TestMethod]
	public void IsWithinRadius_outside_false()
	{
		Assert.IsFalse( VRInteractionRules.IsWithinRadius( 0, 0, 0, 10f, 0, 0, 9f ) );
	}
}
