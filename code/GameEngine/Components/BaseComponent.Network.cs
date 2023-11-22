using Sandbox;
using Sandbox.Network;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public abstract partial class BaseComponent
{
	public bool IsProxy => GameObject.IsProxy;

	public void __rpc_Broadcast( Action resume, string methodName, params object[] argumentList )
	{
		if ( !Rpc.Calling && GameObject.IsNetworked && SceneNetworkSystem.Instance is not null )
		{
			var msg = new ObjectMessageMsg();
			msg.Guid = GameObject.Id;
			msg.Component = GetType().Name;
			msg.MessageName = methodName;
			msg.ArgumentData = TypeLibrary.ToBytes( argumentList );

			SceneNetworkSystem.Instance.Broadcast( msg );
		}

		Rpc.PreCall();

		// we want to call this
		resume();
	}

}
