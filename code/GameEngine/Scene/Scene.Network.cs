using Sandbox;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Sandbox.Network;
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

	RealTimeSince timeSinceNetworkUpdate = 0;

	internal void SceneNetworkUpdate()
	{
		if ( timeSinceNetworkUpdate < (1.0f / 30.0f) )
			return;

		timeSinceNetworkUpdate = 0;

		if ( SceneNetworkSystem.Instance == null )
			return;

		foreach ( var obj in networkedObjects )
		{
			if ( !obj.IsMine ) continue;

			//Log.Info( $"TIK: {obj}" );
			obj.NetworkUpdate();
		}
	}


	internal List<ObjectCreateMsg> SerializeNetworkObjects()
	{
		var jso = new List<ObjectCreateMsg>();

		foreach ( var target in networkedObjects )
		{
			var create = new ObjectCreateMsg();
			create.Guid = target.Id;
			create.JsonData = target.Serialize().ToJsonString();
			create.Owner = target.Net.Owner;
			create.Creator = target.Net.Creator;
			create.Update = target.CreateNetworkUpdate();

			jso.Add( create );
		}

		return jso;
	}


	internal void DestroyNetworkObjects( Func<GameObject, bool> test )
	{
		var found = networkedObjects.Where( test ).ToArray();

		foreach( var f in found )
		{
			f.Destroy();
		}
	}
}
