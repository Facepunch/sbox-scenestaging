using Editor.MovieMaker;

namespace Sandbox.MovieMaker.Test;

[TestClass]
public sealed class TransformTests
{
	[TestMethod]
	[DataRow( 0, 1.0, 1.0 )]
	[DataRow( 0, 2.0, 2.0 )]
	[DataRow( 1200, 1.0, 0.5 )]
	[DataRow( 1200, 2.0, 1.0 )]
	[DataRow( -1200, 1.0, 2.0 )]
	[DataRow( -1200, 2.0, 4.0 )]
	public void TimeScale( int cents, double time, double expected )
	{
		var scale = MovieTimeScale.FromCents( cents );
		var scaled = scale * MovieTime.FromSeconds( time );

		Assert.AreEqual( expected, scaled );
	}

	[TestMethod]
	[DataRow( 0.0, 0 )]
	[DataRow( 1.0, 0 )]
	[DataRow( -1.0, 0 )]
	[DataRow( 0.0, 1200 )]
	[DataRow( 0.0, -1200 )]
	[DataRow( 1.0, 1200 )]
	[DataRow( 1.0, -1200 )]
	[DataRow( -1.0, 1200 )]
	[DataRow( -1.0, -1200 )]
	public void Inverse( double translation, int cents )
	{
		var transform = new MovieTransform( translation, MovieTimeScale.FromCents( cents ) );
		var inverse = transform.Inverse;

		Assert.AreEqual( MovieTransform.Identity, inverse * transform );
		Assert.AreEqual( MovieTransform.Identity, transform * inverse );
	}

	[TestMethod]
	[DataRow( 0.0, 0, 0.0, 0.0 )]
	[DataRow( 1.0, 0, 0.0, 1.0 )]
	[DataRow( 1.0, 0, 1.0, 2.0 )]
	[DataRow( 0.0, 1200, 0.0, 0.0 )]
	[DataRow( 0.0, 1200, 1.0, 0.5 )]
	[DataRow( 1.0, 1200, 0.0, 1.0 )]
	[DataRow( 1.0, 1200, 1.0, 1.5 )]
	public void Transform( double translation, int cents, double time, double expected )
	{
		var transform = new MovieTransform( translation, MovieTimeScale.FromCents( cents ) );

		Assert.AreEqual( expected, transform * time );
	}
}
