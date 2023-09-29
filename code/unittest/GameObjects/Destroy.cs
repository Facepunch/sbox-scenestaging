using Sandbox;
using System.Linq;

namespace GameObjects;

[TestClass]
public class DestroyTests
{
	[TestMethod]
	public void Destroy()
	{
		var scene = new Scene();

		var go = scene.CreateObject();

		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );

		go.Destroy();

		// nothing should change until next tick
		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );

		scene.Tick();

		Assert.IsFalse( go.Enabled );
		Assert.IsFalse( go.Active );
		Assert.IsNull( go.Scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 0, scene.All.Count() );
	}

	[TestMethod]
	public void Destroy_Child()
	{
		var scene = new Scene();

		var parent = scene.CreateObject();
		var go = new GameObject();
		go.Parent = parent;

		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNotNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 1, parent.Children.Count() );

		go.Destroy();

		// nothing should change until next tick
		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNotNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 1, parent.Children.Count() );

		scene.Tick();

		Assert.IsFalse( go.Enabled );
		Assert.IsFalse( go.Active );
		Assert.IsNull( go.Scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 0, parent.Children.Count() );
	}

	[TestMethod]
	public void Destroy_With_Child()
	{
		var scene = new Scene();
		var parent = scene.CreateObject();
		var go = new GameObject();
		go.Parent = parent;

		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNotNull( go.Parent );
		Assert.IsTrue( parent.Enabled );
		Assert.IsTrue( parent.Active );
		Assert.IsNotNull( parent.Scene );
		Assert.AreEqual( parent.Scene, scene );
		Assert.IsNull( parent.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 1, parent.Children.Count() );

		parent.Destroy();

		// nothing should change until next tick
		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNotNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 1, parent.Children.Count() );

		scene.Tick();
		
		Assert.IsFalse( parent.Enabled );
		Assert.IsFalse( parent.Active );
		Assert.IsNull( parent.Scene );
		Assert.IsNull( parent.Parent );
		Assert.IsFalse( go.Enabled );
		Assert.IsFalse( go.Active );
		Assert.IsNull( go.Scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 0, scene.All.Count() );
		Assert.AreEqual( 0, parent.Children.Count() );
	}

	[TestMethod]
	public void Destroy_With_Child_Immediate()
	{
		var scene = new Scene();
		var parent = scene.CreateObject();
		var go = new GameObject();
		go.Parent = parent;

		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNotNull( go.Parent );
		Assert.IsTrue( parent.Enabled );
		Assert.IsTrue( parent.Active );
		Assert.IsNotNull( parent.Scene );
		Assert.AreEqual( parent.Scene, scene );
		Assert.IsNull( parent.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 1, parent.Children.Count() );

		parent.DestroyImmediate();

		Assert.IsFalse( parent.Enabled );
		Assert.IsFalse( parent.Active );
		Assert.IsNull( parent.Scene );
		Assert.IsNull( parent.Parent );
		Assert.IsFalse( go.Enabled );
		Assert.IsFalse( go.Active );
		Assert.IsNull( go.Scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 0, scene.All.Count() );
		Assert.AreEqual( 0, parent.Children.Count() );
	}

	[TestMethod]
	public void Destroy_Child_immediate()
	{
		var scene = new Scene();

		var parent = scene.CreateObject();
		var go = new GameObject();
		go.Parent = parent;

		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNotNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 1, parent.Children.Count() );

		go.DestroyImmediate();

		Assert.IsFalse( go.Enabled );
		Assert.IsFalse( go.Active );
		Assert.IsNull( go.Scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );
		Assert.AreEqual( 0, parent.Children.Count() );

	}

	[TestMethod]
	public void Destroy_Immediate()
	{
		var scene = new Scene();

		var go = scene.CreateObject();

		Assert.IsTrue( go.Enabled );
		Assert.IsTrue( go.Active );
		Assert.IsNotNull( go.Scene );
		Assert.AreEqual( go.Scene, scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 1, scene.All.Count() );

		go.DestroyImmediate();

		Assert.IsFalse( go.Enabled );
		Assert.IsFalse( go.Active );
		Assert.IsNull( go.Scene );
		Assert.IsNull( go.Parent );
		Assert.AreEqual( 0, scene.All.Count() );
	}
}
