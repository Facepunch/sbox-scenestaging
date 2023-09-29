using Sandbox;
using System.Linq;

namespace MyTests;

[TestClass]
public class SceneTest
{
	[TestMethod]
	public void TestMethod1()
	{
		var scene = new Scene();

		Assert.AreEqual( 0, scene.All.Count() );

		var go = scene.CreateObject();

		Assert.AreEqual( 0, go.Components.Count() );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 0, scene.SceneWorld.SceneObjects.Count );

		var model = go.AddComponent<ModelComponent>();
		model.Model = Model.Load( "models/dev/box.vmdl" );
		Assert.IsNotNull( model.Model );
		Assert.AreEqual( 1, go.Components.Count() );
		Assert.AreEqual( 1, scene.SceneWorld.SceneObjects.Count );

		go.Destroy();

		Assert.AreEqual( 1, go.Components.Count() );
		Assert.AreEqual( 1, scene.SceneWorld.SceneObjects.Count );

		scene.Tick();

		Assert.AreEqual( 1, go.Components.Count() );
		Assert.AreEqual( 0, scene.SceneWorld.SceneObjects.Count );
	}
}
