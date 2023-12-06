using Sandbox;

namespace GameObjects;

[TestClass]
public class SerializeTest
{
	[TestMethod]
	public void SerializeSingle()
	{
		Assert.IsNotNull( TypeLibrary, "TypeLibrary hasn't been mocked" );
		Assert.IsNotNull( TypeLibrary.GetType<ModelRenderer>(), "TypeLibrary hasn't been given the game assembly" );

		using var scope =  new Scene().Push();

		var go1 = new GameObject	();
		go1.Name = "My Game Object";
		go1.Transform.Local = new Transform( Vector3.Up, Rotation.Identity, 10 );

		var model = go1.Components.Create<ModelRenderer>();
		model.Model = Model.Load( "models/dev/box.vmdl" );
		model.Tint = Color.Red;

		var node = go1.Serialize();

		System.Console.WriteLine( node );

		SceneUtility.MakeGameObjectsUnique( node );

		var go2 = new GameObject();
		go2.Deserialize( node );

		Assert.AreNotEqual( go1.Id, go2.Id );
		Assert.AreEqual( go1.Name, go2.Name );
		Assert.AreEqual( go1.Enabled, go2.Enabled );
		Assert.AreEqual( go1.Transform.Local, go2.Transform.Local );
		Assert.AreEqual( go1.Components.Count, go2.Components.Count );
		Assert.AreEqual( go1.Components.Get<ModelRenderer>().Model, go2.Components.Get<ModelRenderer>().Model );
		Assert.AreEqual( go1.Components.Get<ModelRenderer>().Tint, go2.Components.Get<ModelRenderer>().Tint );
		Assert.AreEqual( go1.Components.Get<ModelRenderer>().MaterialOverride, go2.Components.Get<ModelRenderer>().MaterialOverride );
	}

	[TestMethod]
	public void SerializeWithChildren()
	{
		using var scope = new Scene().Push();

		var timer = new ScopeTimer( "Creation" );
		var go1 = new GameObject();
		go1.Name = "My Game Object";
		go1.Transform.Local = new Transform( Vector3.Up, Rotation.Identity, 10 );

		int childrenCount = 15000;

		for ( int i = 0; i < childrenCount; i++ )
		{
			var child = new GameObject();
			child.Name = $"Child {i}";
			child.Transform.Local = new Transform( Vector3.Random * 1000 );
			child.Parent = go1;

			child.Components.Create<ModelRenderer>();
		}

		timer.Dispose();
		timer = new ScopeTimer( "Serialize" );

		var node = go1.Serialize();


		timer.Dispose();

		using ( new ScopeTimer( "MakeGameObjectsUnique" ) )
		{
			SceneUtility.MakeGameObjectsUnique( node );
		}

		timer = new ScopeTimer( "Deserialize" );
		//System.Console.WriteLine( node );

		var go2 = new GameObject();
		go2.Deserialize( node );

		timer.Dispose();

		Assert.AreNotEqual( go1.Id, go2.Id );
		Assert.AreEqual( go1.Name, go2.Name );
		Assert.AreEqual( go1.Enabled, go2.Enabled );
		Assert.AreEqual( go1.Transform.Local, go2.Transform.Local );
		Assert.AreEqual( go2.Children.Count, childrenCount );


	}
}
