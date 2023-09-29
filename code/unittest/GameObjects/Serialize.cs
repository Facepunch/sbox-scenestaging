using Sandbox;

namespace GameObjects;

[TestClass]
public class SerializeTest
{
	[TestMethod]
	public void SerializeSingle()
	{
		Assert.IsNotNull( TypeLibrary, "TypeLibrary hasn't been mocked" );
		Assert.IsNotNull( TypeLibrary.GetType<ModelComponent>(), "TypeLibrary hasn't been given the game assembly" );

		var go1 = new GameObject();
		go1.Name = "My Game Object";
		go1.Transform = new Transform( Vector3.Up, Rotation.Identity, 10 );

		var model = go1.AddComponent<ModelComponent>();
		model.Model = Model.Load( "models/dev/box.vmdl" );
		model.Tint = Color.Red;

		var node = go1.Serialize();

		System.Console.WriteLine( node );

		var go2 = new GameObject();
		go2.Deserialize( node );

		Assert.AreEqual( go1.Id, go2.Id );
		Assert.AreEqual( go1.Name, go2.Name );
		Assert.AreEqual( go1.Enabled, go2.Enabled );
		Assert.AreEqual( go1.Transform, go2.Transform );
		Assert.AreEqual( go1.Components.Count, go2.Components.Count );
		Assert.AreEqual( go1.GetComponent<ModelComponent>().Model, go2.GetComponent<ModelComponent>().Model );
		Assert.AreEqual( go1.GetComponent<ModelComponent>().Tint, go2.GetComponent<ModelComponent>().Tint );
		Assert.AreEqual( go1.GetComponent<ModelComponent>().MaterialOverride, go2.GetComponent<ModelComponent>().MaterialOverride );
	}

	[TestMethod]
	public void SerializeWithChildren()
	{
		var timer = new ScopeTimer( "Creation" );
		var go1 = new GameObject();
		go1.Name = "My Game Object";
		go1.Transform = new Transform( Vector3.Up, Rotation.Identity, 10 );

		int childrenCount = 150000;

		for ( int i = 0; i < childrenCount; i++ )
		{
			var child = new GameObject();
			child.Name = $"Child {i}";
			child.Transform = new Transform( Vector3.Random * 1000 );
			child.Parent = go1;

			child.AddComponent<ModelComponent>();
		}

		timer.Dispose();
		timer = new ScopeTimer( "Serialize" );

		var node = go1.Serialize();


		timer.Dispose();
		timer = new ScopeTimer( "Deserialize" );
		//System.Console.WriteLine( node );

		var go2 = new GameObject();
		go2.Deserialize( node );

		timer.Dispose();

		Assert.AreEqual( go1.Id, go2.Id );
		Assert.AreEqual( go1.Name, go2.Name );
		Assert.AreEqual( go1.Enabled, go2.Enabled );
		Assert.AreEqual( go1.Transform, go2.Transform );
		Assert.AreEqual( go2.Children.Count, childrenCount );


	}
}
