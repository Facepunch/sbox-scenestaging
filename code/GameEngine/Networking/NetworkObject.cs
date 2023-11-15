using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Network;
using System.Text.Json.Nodes;

public sealed class NetworkObject : BaseComponent
{
	public Guid Creator { get; set; }
	public Guid Owner { get; set; }

	public bool IsMine => Owner == SceneNetworkSystem.LocalId;

	public override void OnAwake()
	{
		base.OnAwake();


	}

	public override void OnStart()
	{
		base.OnStart();
	}

	public override void Update()
	{

	}

	public static void Instantiate( GameObject target )
	{
		if ( SceneNetworkSystem.Instance is not null )
		{
			var create = new ObjectCreateMsg();
			create.Guid = target.Id;
			create.JsonData = target.Serialize()?.ToJsonString() ?? "{}";
			create.Owner = SceneNetworkSystem.LocalId;
			create.Creator = SceneNetworkSystem.LocalId;

			SceneNetworkSystem.Instance.Broadcast( create );
		}

		var netObject = target.GetComponent<NetworkObject>();
		netObject.Creator = SceneNetworkSystem.LocalId;
		netObject.Owner = SceneNetworkSystem.LocalId;

		target.SetNetworkObject( netObject );
	}
}
