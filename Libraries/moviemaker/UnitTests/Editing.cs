using System;
using Editor.MovieMaker;
using static Editor.MovieMaker.TimeSelection;

namespace Sandbox.MovieMaker.Test;

[TestClass]
public sealed class EditingTests
{
	private static PropertySignal<float> CreateCrossFadeWithRange( MovieTimeRange fadeRange )
	{
		var from = 1f.AsSignal();
		var to = 2f.AsSignal();

		return from.CrossFade( to, fadeRange );
	}

	[TestMethod]
	public void SimpleCrossFade()
	{
		var fade = CreateCrossFadeWithRange( (0d, 1d) );

		Assert.AreEqual( 1f, fade.GetValue( 0 ) );
		Assert.AreEqual( 1.5f, fade.GetValue( 0.5 ) );
		Assert.AreEqual( 2f, fade.GetValue( 1 ) );
	}

	[TestMethod]
	public void CrossFadeWithInnerRange()
	{
		var fade = CreateCrossFadeWithRange( (0.25d, 0.75d) );

		Assert.AreEqual( 1f, fade.GetValue( 0 ) );
		Assert.AreEqual( 1f, fade.GetValue( 0.25 ) );
		Assert.AreEqual( 1.5f, fade.GetValue( 0.5 ) );
		Assert.AreEqual( 2f, fade.GetValue( 0.75 ) );
		Assert.AreEqual( 2f, fade.GetValue( 1 ) );
	}

	[TestMethod]
	public void OverlappingCrossFade()
	{
		var fade1 = CreateCrossFadeWithRange( (0d, 0.75d) );
		var fade2 = CreateCrossFadeWithRange( (0.25d, 1d) );

		var fade = fade1.CrossFade( fade2, (0d, 1d) );

		Console.WriteLine( fade.ToString() );

		Assert.AreEqual( 1f, fade.GetValue( 0d ) );
		Assert.AreEqual( 2f, fade.GetValue( 1d ) );
	}

	[TestMethod]
	public void HardCut()
	{
		var signal = 1f.AsSignal().HardCut( 2f, 1d );

		Console.WriteLine( signal.ToString() );

		Assert.AreEqual( 1f, signal.GetValue( 0d ) );
		Assert.AreEqual( 1f, signal.GetValue( 1d - MovieTime.Epsilon ) );
		Assert.AreEqual( 2f, signal.GetValue( 1d ) );
	}

	[TestMethod]
	public void Blend()
	{
		var signal = 1f.AsSignal().Blend( 2f, 0.5f );

		Console.WriteLine( signal.ToString() );

		Assert.AreEqual( 1.5f, signal.GetValue( default ) );
	}
}
