using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sandbox.Solids.Test;

[TestClass]
public class PrimitiveTest
{
	[TestMethod]
	public void Box()
	{
		var box = Solid.Box( default, new Vertex( 1024, 1024, 1024 ), null! );

		Assert.AreEqual( 8, box.VertexCount );
		Assert.AreEqual( 5, box.CellCount );
	}
}
