using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sandbox.Animation.Tests;

#nullable enable

[TestClass]
public class KeyframeCurveTests
{
	/// <summary>
	/// Change between <see cref="float"/> values with no interpolation.
	/// </summary>
	[TestMethod]
	public void SimpleFloatNoInterpolation()
	{
		var curve = new KeyframeCurve<float>();
		var easing = KeyframeEasing.None;

		curve.SetKeyframe( 0f, 0f, easing );
		curve.SetKeyframe( 1f, 1f, easing );
		curve.SetKeyframe( 2f, 0f, easing );

		Assert.AreEqual( 0f, curve.GetValue( 0f ) );
		Assert.AreEqual( 1f, curve.GetValue( 1f ) );
		Assert.AreEqual( 0f, curve.GetValue( 0.5f ) );
		Assert.AreEqual( 1f, curve.GetValue( 1.5f ) );
	}

	/// <summary>
	/// Linearly interpolate between <see cref="float"/> values.
	/// </summary>
	[TestMethod]
	public void SimpleFloatLinear()
	{
		var curve = new KeyframeCurve<float>();
		var easing = KeyframeEasing.Linear;

		curve.SetKeyframe( 0f, 0f, easing );
		curve.SetKeyframe( 1f, 1f, easing );
		curve.SetKeyframe( 2f, -1f, easing );

		Assert.AreEqual( 0f, curve.GetValue( 0f ) );
		Assert.AreEqual( 1f, curve.GetValue( 1f ) );
		Assert.AreEqual( 0.5f, curve.GetValue( 0.5f ) );
		Assert.AreEqual( 0f, curve.GetValue( 1.5f ) );
		Assert.AreEqual( 0f, curve.GetValue( -1f ) );
		Assert.AreEqual( -1f, curve.GetValue( 3f ) );
	}

	/// <summary>
	/// Linearly interpolate between <see cref="Vector3"/> values.
	/// </summary>
	[TestMethod]
	public void SimpleVector3Linear()
	{
		var curve = new KeyframeCurve<Vector3>();
		var easing = KeyframeEasing.Linear;

		curve.SetKeyframe( 0f, 0f, easing );
		curve.SetKeyframe( 1f, new Vector3( 100f, -200f, 300f ), easing );
		curve.SetKeyframe( 2f, 0f, easing );

		Assert.AreEqual( 0f, curve.GetValue( 0f ) );
		Assert.AreEqual( new Vector3( 100f, -200f, 300f ), curve.GetValue( 1f ) );
		Assert.AreEqual( new Vector3( 50f, -100f, 150f ), curve.GetValue( 0.5f ) );
		Assert.AreEqual( new Vector3( 50f, -100f, 150f ), curve.GetValue( 1.5f ) );
	}

	/// <summary>
	/// Change between values that can't be interpolated.
	/// </summary>
	[TestMethod]
	public void SimpleNonInterpolatable()
	{
		var curve = new KeyframeCurve<string>();
		var easing = KeyframeEasing.Linear;

		curve.SetKeyframe( 0f, "Hello", easing );
		curve.SetKeyframe( 1f, "World", easing );
		curve.SetKeyframe( 2f, "Hello", easing );

		Assert.AreEqual( "Hello", curve.GetValue( 0f ) );
		Assert.AreEqual( "World", curve.GetValue( 1f ) );
		Assert.AreEqual( "Hello", curve.GetValue( 0.5f ) );
		Assert.AreEqual( "World", curve.GetValue( 1.5f ) );
	}
}
