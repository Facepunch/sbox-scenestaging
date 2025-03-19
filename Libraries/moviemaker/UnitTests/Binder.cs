using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker.Properties;

namespace Sandbox.MovieMaker.Test;

#nullable enable

[TestClass]
public sealed class BinderTests : SceneTests
{
	/// <summary>
	/// Game object tracks without an explicit binding must auto-bind to root objects
	/// in the current scene with a matching name.
	/// </summary>
	[TestMethod]
	public void BindRootGameObjectMatchingName()
	{
		var exampleObject = new GameObject( true, "Example" );
		var exampleTrack = CompiledClip.RootGameObject( exampleObject.Name );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsTrue( target.IsBound );
		Assert.AreEqual( exampleObject, target.Value );
	}

	/// <summary>
	/// Don't auto-bind to a root object with a different name.
	/// </summary>
	[TestMethod]
	public void BindRootGameObjectNoMatchingName()
	{
		var exampleObject = new GameObject( true, "Examble" );
		var exampleTrack = CompiledClip.RootGameObject( "Example" );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsFalse( target.IsBound );
	}

	/// <summary>
	/// We can bind to a game object if it changes name to match the track.
	/// </summary>
	[TestMethod]
	public void LateBindRootGameObjectMatchingName()
	{
		var exampleObject = new GameObject( true, "Examble" );
		var exampleTrack = CompiledClip.RootGameObject( "Example" );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsFalse( target.IsBound );

		exampleObject.Name = "Example";

		Assert.IsTrue( target.IsBound );
		Assert.AreEqual( exampleObject, target.Value );
	}

	/// <summary>
	/// Bindings will persist, even if the bound object changes name.
	/// </summary>
	[TestMethod]
	public void StickyBinding()
	{
		var exampleObject = new GameObject( true, "Example" );
		var exampleTrack = CompiledClip.RootGameObject( exampleObject.Name );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsTrue( target.IsBound );
		Assert.AreEqual( exampleObject, target.Value );

		exampleObject.Name = "Examble";

		Assert.IsTrue( target.IsBound );
		Assert.AreEqual( exampleObject, target.Value );

		target.Reset();

		Assert.IsFalse( target.IsBound );
	}

	/// <summary>
	/// We can manually bind a track to a particular object.
	/// </summary>
	[TestMethod]
	public void ExplicitBinding()
	{
		var exampleObject = new GameObject( true, "Examble" );
		var exampleTrack = CompiledClip.RootGameObject( "Example" );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsFalse( target.IsBound );

		target.Bind( exampleObject );

		Assert.IsTrue( target.IsBound );
		Assert.AreEqual( exampleObject, target.Value );
	}

	/// <summary>
	/// Properties are bound based on their parent track's binding.
	/// </summary>
	[TestMethod]
	public void PropertyBinding()
	{
		var exampleTrack = CompiledClip.RootGameObject( "Example" )
			.Property<Vector3>( nameof(GameObject.LocalPosition) );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsFalse( target.IsBound );

		var exampleObject = new GameObject( true, "Example" );

		Assert.IsTrue( target.IsBound );

		target.Value = new Vector3( 100, 200, 300 );

		Assert.AreEqual( new Vector3( 100, 200, 300 ), exampleObject.LocalPosition );
	}

	/// <summary>
	/// Properties are bound based on their parent track's binding.
	/// </summary>
	[TestMethod]
	public void SubPropertyBinding()
	{
		var exampleTrack = CompiledClip.RootGameObject( "Example" )
			.Property<Vector3>( nameof(GameObject.LocalPosition) )
			.Property<float>( nameof(Vector3.y) );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsFalse( target.IsBound );

		var exampleObject = new GameObject( true, "Example" );

		Assert.IsTrue( target.IsBound );

		target.Value = 100f;

		Assert.AreEqual( new Vector3( 0, 100, 0 ), exampleObject.LocalPosition );
	}

	/// <summary>
	/// Support custom <see cref="ITrackPropertyFactory"/> implementations.
	/// </summary>
	[TestMethod]
	public void CustomPropertyBinding()
	{
		var exampleObject = new GameObject( true, "Example" );
		var exampleTrack = CompiledClip.RootGameObject( "Example" )
			.Property<Vector3>( "LookAt" );

		var target = TrackBinder.Default.Get( exampleTrack );

		Assert.IsTrue( target.IsBound );

		target.Value = new Vector3( 100f, 0f, 0f );

		Assert.IsTrue( new Vector3( 1f, 0f, 0f ).AlmostEqual( exampleObject.WorldRotation.Forward ) );

		target.Value = new Vector3( 0f, -100f, 0f );

		Assert.IsTrue( new Vector3( 0f, -1f, 0f ).AlmostEqual( exampleObject.WorldRotation.Forward ) );
	}
}
