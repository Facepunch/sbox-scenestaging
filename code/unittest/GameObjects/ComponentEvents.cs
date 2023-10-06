using Sandbox;
using System.Linq;

namespace GameObjects;

[TestClass]
public class ComponentEvents
{
	[TestMethod]
	public void Single()
	{
		var scene = new Scene();
		using var sceneScope = scene.Push();

		var go = scene.CreateObject();
		var o = go.AddComponent<OrderTestComponent>();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 0, o.DisabledCalls );

		scene.GameTick();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 0, o.DisabledCalls );

		go.Enabled = false;
		scene.GameTick();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 1, o.DisabledCalls );

		go.Destroy();
		scene.GameTick();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 1, o.DisabledCalls );
	}

	[TestMethod]
	public void Single_Destroy()
	{
		var scene = new Scene();
		using var sceneScope = scene.Push();

		var go = scene.CreateObject();
		var o = go.AddComponent<OrderTestComponent>();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 0, o.DisabledCalls );

		scene.GameTick();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 0, o.DisabledCalls );

		go.Destroy();
		scene.GameTick();

		Assert.AreEqual( 1, o.EnabledCalls );
		Assert.AreEqual( 1, o.DisabledCalls );
	}
}

public class OrderTestComponent : BaseComponent
{
	public int EnabledCalls;
	public int DisabledCalls;

	public override void OnEnabled()
	{
		EnabledCalls++;
	}

	public override void OnDisabled()
	{
		DisabledCalls++;
	}
}
