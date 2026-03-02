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
}
