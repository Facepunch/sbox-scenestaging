using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker.Test;

[TestClass]
public class Signals
{
	/// <summary>
	/// Calling Reduce() on a transformed signal would throw a stack overflow.
	/// </summary>
	[TestMethod]
	public void ReduceTransformedSignal()
	{
		var signal = PropertySignal.FromSamples( 0d, 1, [0f, 0f, 1f, 1f, 2f, 1f, 0f, 1f, 1f, 0f] );

		var transformed = new MovieTransform( 1d ) * signal;

		Assert.AreNotSame( transformed, signal );

		var reduced = transformed.Reduce( 0, 10 );

		Assert.AreSame( transformed, reduced );
	}

	/// <summary>
	/// We can snip out keyframes when reducing to a time range.
	/// </summary>
	[TestMethod]
	public void ReduceKeyframesSimple()
	{
		var signal = PropertySignal.FromKeyframes( [
			new Keyframe<int>( 0.0, 100, KeyframeInterpolation.Linear ),
			new Keyframe<int>( 1.0, 200, KeyframeInterpolation.Linear ),
			new Keyframe<int>( 2.0, 300, KeyframeInterpolation.Linear )
		] );

		var reduced = signal.Reduce( 0.0, 1.0 );

		Assert.IsInstanceOfType<IKeyframeSignal>( reduced );
		Assert.AreEqual( 2, reduced.Keyframes.Count );
		Assert.AreEqual( 0.0, reduced.Keyframes[0].Time );
		Assert.AreEqual( 1.0, reduced.Keyframes[1].Time );
	}
}
