using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;


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
		_destroying = true;

		ForEachComponent( "OnDestroy", false, c => c.Destroy() );
		ForEachChild( "Children", false, c => c.Term() );

		Children.RemoveAll( x => x is null );
		Components.RemoveAll( x => x is null );

		Assert.AreEqual( 0, Components.Count, "Some components weren't deleted!" );
		Assert.AreEqual( 0, Children.Count, "Some children weren't deleted!" );

		_destroyed = true;
		Scene.Directory.Remove( this );
		Enabled = false;
		Parent = null;
		Scene = null;
		_destroyed = true;
	}

	/// <summary>
	/// Destroy this object. Will actually be destroyed at the start of the next frame.
	/// </summary>
	public void Destroy()
	{
		if ( _destroying )
			return;

		_destroying = true;

		Scene?.QueueDelete( this );
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
	public void Clear()
	{
		// delete all components
		ForEachComponent( "OnDestroy", false, c => c.Destroy() );

		// delete all children
		ForEachChild( "Children", false, c => c.Term() );

		Components.RemoveAll( x => x is null );
		Children.RemoveAll( x => x is null );

		Assert.AreEqual( 0, Components.Count, $"{Components.Count} components weren't deleted!" );
		Assert.AreEqual( 0, Children.Count, $"{Children.Count} children weren't deleted!" );
	}
}
