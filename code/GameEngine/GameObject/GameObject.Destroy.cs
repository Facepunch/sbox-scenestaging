using Sandbox;
using Sandbox.Diagnostics;


public partial class GameObject : IValid
{
	bool _destroying;
	bool _destroyed;

	/// <summary>
	/// True if the GameObject is not destroyed
	/// </summary>
	public bool IsValid => !_destroyed && Scene is not null;

	/// <summary>
	/// Actually destroy the object and its children. Turn off and destroy components.
	/// </summary>
	private void Term()
	{
		using var batch = CallbackBatch.StartGroup();
		_destroying = true;

		Components.ForEach( "OnDestroy", true, c => c.Destroy() );
		ForEachChild( "Children", true, c => c.Term() );

		CallbackBatch.Add( CommonCallback.Term, TermFinal, this, "Term" );
	}

	/// <summary>
	/// The last thing ever called.
	/// </summary>
	private void TermFinal()
	{
		_destroyed = true;

		EndNetworking();
		Scene.Directory.Remove( this );
		Enabled = false;
		Parent = null;
		Scene = null;

		Children.RemoveAll( x => x is null );
		Components.RemoveNull();

		Assert.AreEqual( 0, Components.Count, "Some components weren't deleted!" );
		Assert.AreEqual( 0, Children.Count, "Some children weren't deleted!" );
	}

	/// <summary>
	/// Destroy this object. Will actually be destroyed at the start of the next frame.
	/// </summary>
	public virtual void Destroy()
	{
		if ( _destroying )
			return;

		_destroying = true;

		Scene?.QueueDelete( this );
		_net?.SendNetworkDestroy();
	}

	/// <summary>
	/// Destroy this object immediately. Calling this might cause some problems if functions
	/// are expecting the object to still exist, so it's not always a good idea.
	/// </summary>
	public void DestroyImmediate()
	{
		Term();
	}

	/// <summary>
	/// Remove all children
	/// </summary>
	/// <param name="child"></param>
	private void RemoveChild( GameObject child )
	{
		var i = Children.IndexOf( child );
		if ( i < 0 ) return;

		Children.RemoveAt( i );
	}

	/// <summary>
	/// Destroy all components and child objects
	/// </summary>
	public virtual void Clear()
	{
		// delete all components
		Components.ForEach( "OnDestroy", true, c => c.Destroy() );

		// delete all children
		ForEachChild( "Children", true, c => c.Term() );

		Components.RemoveNull();
		
		Children.RemoveAll( x => x is null );

		Assert.AreEqual( 0, Components.Count, $"{Components.Count} components weren't deleted!" );
		Assert.AreEqual( 0, Children.Count, $"{Children.Count} children weren't deleted!" );
	}
}
