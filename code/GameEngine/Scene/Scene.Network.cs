using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Utility;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Scene : GameObject
{
	HashSet<GameObject> networkedObjects = new HashSet<GameObject>();

	internal void RegisterNetworkedObject( GameObject obj )
	{
		networkedObjects.Add( obj );
	}

	internal void UnregisterNetworkObject( GameObject obj )
	{
		networkedObjects.Remove( obj );
	}

	TimeSince timeSinceNetworkUpdate = 0;

	internal void SceneNetworkUpdate()
	{
		if ( timeSinceNetworkUpdate < ( 1.0f / 30.0f ) )
			return;

		timeSinceNetworkUpdate = 0;

		foreach( var obj in networkedObjects )
		{
			if ( !obj.IsMine ) continue;

			obj.NetworkUpdate();
		}
	}
}
