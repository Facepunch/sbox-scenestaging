using Sandbox;
using Sandbox.Network;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public abstract partial class Component
{
	public GameObject.NetworkAccessor Network => GameObject.Network;

	public bool IsProxy => GameObject.IsProxy;

	public void __rpc_Broadcast( Action resume, string methodName, params object[] argumentList )
	{
		if ( !Rpc.Calling && Network.Active && SceneNetworkSystem.Instance is not null )
		{
			var msg = new ObjectMessageMsg();
			msg.Guid = GameObject.Id;
			msg.Component = GetType().Name;
			msg.MessageName = methodName;
			msg.Arguments = argumentList;

			SceneNetworkSystem.Instance.Broadcast( msg );
		}

		Rpc.PreCall();

		// we want to call this
		resume();
	}

}
