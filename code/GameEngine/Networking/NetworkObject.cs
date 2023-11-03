using Sandbox;
using static Sandbox.PhysicsContact;

public sealed class NetworkObject : BaseComponent
{
	public Guid Creator { get; set; }
	public Guid Owner { get; set; }

	public bool IsMine => Owner == SceneNetworkSystem.LocalGuid();

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
		var create = new Net_ObjectCreate();
		create.Guid = target.Id;
		create.JsonData = target.Serialize();
		create.Owner = SceneNetworkSystem.LocalGuid();
		create.Creator = SceneNetworkSystem.LocalGuid();

		SceneNetworkSystem.Instance.BroadcastJson( create );

		var netObject = target.GetComponent<NetworkObject>();
		netObject.Creator = create.Creator;
		netObject.Owner = create.Owner;

		target.SetNetworkObject( netObject );
	}

	public static void CreateFromWire( NetworkUser user, Net_ObjectCreate create )
	{
		using var scope = GameManager.ActiveScene.Push();

		Log.Info( $"OnObjectCreate from {user}" );

		var go = new GameObject();
		go.Deserialize( create.JsonData );

		var netObject = go.GetComponent<NetworkObject>();
		netObject.Creator = create.Creator;
		netObject.Owner = create.Owner;

		go.SetNetworkObject( netObject );
	}
}
