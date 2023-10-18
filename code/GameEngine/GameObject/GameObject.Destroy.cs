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
		ForEachChild( "Children", true, c => c.Term() );

		Children.RemoveAll( x => x is null );
		Components.RemoveAll( x => x is null );

		Assert.AreEqual( 0, Components.Count, "Some components weren't deleted!" );
		Assert.AreEqual( 0, Children.Count, "Some children weren't deleted!" );

		_destroyed = true;
		Enabled = false;
		Scene.UnregisterGameObjectId( this );

		Parent = null;
		Enabled = false;
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
}
