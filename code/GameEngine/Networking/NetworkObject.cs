using Sandbox;
using Sandbox.Diagnostics;
using static Sandbox.PhysicsContact;

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
			var create = new Net_ObjectCreate();
			create.Guid = target.Id;
			create.JsonData = target.Serialize();
			create.Owner = SceneNetworkSystem.LocalId;
			create.Creator = SceneNetworkSystem.LocalId;

			SceneNetworkSystem.Instance.BroadcastJson( create );
		}

		var netObject = target.GetComponent<NetworkObject>();
		netObject.Creator = SceneNetworkSystem.LocalId;
		netObject.Owner = SceneNetworkSystem.LocalId;

		target.SetNetworkObject( netObject );
	}

	public static void CreateFromWire( NetworkChannel user, Net_ObjectCreate create )
	{
		Assert.NotNull( GameManager.ActiveScene );

		Log.Info( $"OnObjectCreate from {user} / {GameManager.ActiveScene}" );

		var go = new GameObject();
		go.Deserialize( create.JsonData );

		var netObject = go.GetComponent<NetworkObject>();
		netObject.Creator = create.Creator;
		netObject.Owner = create.Owner;

		go.SetNetworkObject( netObject );
	}
}
