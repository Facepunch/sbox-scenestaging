using System;
using System.Linq;

namespace Sandbox.MovieMaker.Test;

#nullable enable

[TestClass]
public sealed class MovieTimeTests
{
	[TestMethod]
	public void TimeRangeListUnionWithEmpty()
	{
		var first = new MovieTimeRange[] { new( 0d, 1d ) };
		var second = Array.Empty<MovieTimeRange>();

		var union = first.Union( second ).ToArray();

		Assert.AreEqual( 1, union.Length );
		Assert.AreEqual( 0d, union[0].Start );
		Assert.AreEqual( 1d, union[0].End );
	}
}
