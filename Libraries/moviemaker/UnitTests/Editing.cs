using System;
using Editor.MovieMaker;

namespace Sandbox.MovieMaker.Test;

[TestClass]
public sealed class EditingTests
{
	[TestMethod]
	public void SimpleCrossFade()
	{
		var timeRange = new MovieTimeRange( 0d, 1d );
		var from = PropertyBlock<float>.Constant( 1f ).Slice( timeRange );
		var to = PropertyBlock<float>.Constant( 2f ).Slice( timeRange );

		var fade = from.CrossFade( to );

		Console.WriteLine( fade.Serialize() );

		Assert.AreEqual( timeRange, fade.TimeRange );
		Assert.AreEqual( 1f, fade.GetValue( 0 ) );
		Assert.AreEqual( 1.5f, fade.GetValue( 0.5 ) );
		Assert.AreEqual( 2f, fade.GetValue( 1 ) );
	}

	private static PropertyBlock<float> CreateCrossFadeWithRange( MovieTimeRange timeRange, MovieTimeRange fadeRange )
	{
		var from = PropertyBlock<float>.Constant( 1f ).Slice( timeRange );
		var to = PropertyBlock<float>.Constant( 2f ).Slice( timeRange );

		return from.CrossFade( to, fadeRange );
	}

	[TestMethod]
	public void CrossFadeWithInnerRangeCorrectTimeRange()
	{
		var fade = CreateCrossFadeWithRange( (0d, 1d), (0.25d, 0.75d) );

		Assert.AreEqual( (0d, 1d), fade.TimeRange );
	}

	[TestMethod]
	public void CrossFadeWithInnerRangeCorrectValues()
	{
		var fade = CreateCrossFadeWithRange( (0d, 1d), (0.25d, 0.75d) );

		Assert.AreEqual( 1f, fade.GetValue( 0 ) );
		Assert.AreEqual( 1f, fade.GetValue( 0.25 ) );
		Assert.AreEqual( 1.5f, fade.GetValue( 0.5 ) );
		Assert.AreEqual( 2f, fade.GetValue( 0.75 ) );
		Assert.AreEqual( 2f, fade.GetValue( 1 ) );
	}

	[TestMethod]
	public void CrossFadeWithInnerRangeParts()
	{
		var fade = CreateCrossFadeWithRange( (0d, 1d), (0.25d, 0.75d) );

		Console.WriteLine( fade.Serialize() );

		var split = fade.TrySplit()!;

		Assert.AreEqual( 3, split.Count );
		Assert.AreEqual( (0d, 0.25d), split[0].TimeRange );
		Assert.AreEqual( (0.25d, 0.75d), split[1].TimeRange );
		Assert.AreEqual( (0.75d, 1d), split[2].TimeRange );

	}

	[TestMethod]
	public void CrossFadeWithPartialRangeHasParts()
	{
		var fade = CreateCrossFadeWithRange( (0d, 1d), (0.5d, 1d) );

		Console.WriteLine( fade.Serialize() );

		var split = fade.TrySplit()!;

		Assert.AreEqual( 2, split.Count );
		Assert.AreEqual( (0d, 0.5d), split[0].TimeRange );
		Assert.AreEqual( (0.5d, 1d), split[1].TimeRange );
	}

	[TestMethod]
	public void OverlappingCrossFade()
	{
		var fade1 = CreateCrossFadeWithRange( (0d, 1d), (0d, 0.75d) );
		var fade2 = CreateCrossFadeWithRange( (0d, 1d), (0.25d, 1d) );

		var fade = fade1.CrossFade( fade2 );

		Console.WriteLine( fade.Serialize() );

		var split = fade.TrySplit()!;

		Assert.AreEqual( 3, split.Count );
		Assert.AreEqual( (0d, 0.25d), split[0].TimeRange );
		Assert.AreEqual( (0.25d, 0.75d), split[1].TimeRange );
		Assert.AreEqual( (0.75d, 1d), split[2].TimeRange );
	}
}
