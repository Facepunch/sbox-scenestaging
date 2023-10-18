using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Scene : GameObject
{
	HashSet<GameObject> deleteList = new();

	/// <summary>
	/// Adds a GameObject to delete later
	/// </summary>
	internal void QueueDelete( GameObject gameObject )
	{
		deleteList.Add( gameObject );
	}

	/// <summary>
	/// Delete any GameObjects waiting to be deleted
	/// </summary>
	public void ProcessDeletes()
	{
		if ( deleteList.Count == 0 )
			return;

		foreach ( var o in deleteList.ToArray() )
		{
			o.DestroyImmediate();
			deleteList.Remove( o );
		}
	}
}
