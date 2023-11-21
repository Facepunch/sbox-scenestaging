using Sandbox;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Sandbox.Network;
using System.Linq;

public partial class Scene : GameObject
{
	/// <summary>
	/// How many times a second network update runs
	/// </summary>
	[Property] public float NetworkFrequency { get; set; } = 60.0f;

	/// <summary>
	/// One divided by NetworkFrequency.
	/// </summary>
	public float NetworkRate => 1.0f / NetworkFrequency;

	HashSet<NetworkObject> networkedObjects = new HashSet<NetworkObject>();

	internal void RegisterNetworkedObject( NetworkObject obj )
	{
		networkedObjects.Add( obj );
	}

	internal void UnregisterNetworkObject( NetworkObject obj )
	{
		networkedObjects.Remove( obj );
	}

	RealTimeSince timeSinceNetworkUpdate = 0;

	internal void SceneNetworkUpdate()
	{
		if ( timeSinceNetworkUpdate < NetworkRate )
			return;

		timeSinceNetworkUpdate = 0;

		if ( SceneNetworkSystem.Instance == null )
			return;

		foreach ( var obj in networkedObjects )
		{
			obj.NetworkUpdate();
		}
	}


	internal List<ObjectCreateMsg> SerializeNetworkObjects()
	{
		var jso = new List<ObjectCreateMsg>();

		foreach ( var target in networkedObjects )
		{
			jso.Add( target.GetCreateMessage( true ) );
		}

		return jso;
	}


	internal void DestroyNetworkObjects( Func<NetworkObject, bool> test )
	{
		var found = networkedObjects.Where( test ).ToArray();

		foreach( var f in found )
		{
			f.Destroy();
		}
	}
}
