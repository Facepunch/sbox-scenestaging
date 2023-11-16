using Sandbox;
using Sandbox.Network;
using Sandbox.Utility;
using System.Runtime;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// This is created and referenced by the network system, as a way to route.
/// </summary>
public class SceneNetworkSystem : GameNetworkSystem
{
	internal static SceneNetworkSystem Instance { get; private set; }

	public SceneNetworkSystem()
	{
		Instance = this;
		Log.Info( "SceneNetworkSystem Initialized" );
	}

	/// <summary>
	/// A client has joined and wants a snapshot of the world
	/// </summary>
	public override void GetSnapshot( NetworkChannel source, ref SnapshotMsg msg )
	{
		ThreadSafe.AssertIsMainThread();

		msg.Time = Time.Now;

		var o = new GameObject.SerializeOptions();
		o.SceneForNetwork = true;

		msg.SceneData = GameManager.ActiveScene.Serialize( o ).ToJsonString();
		msg.NetworkObjects = GameManager.ActiveScene.SerializeNetworkObjects();
	}

	/// <summary>
	/// We have recieved a snapshot of the world
	/// </summary>
	public override async Task SetSnapshotAsync( SnapshotMsg msg )
	{
		ThreadSafe.AssertIsMainThread();

		// TODO - really shouldn't allow set and get of ActiveScene
		// we should have a switchscene function, that disables everything
		// in the old scene.

		GameManager.ActiveScene?.Clear();
		GameManager.ActiveScene?.Destroy();
		GameManager.ActiveScene?.ProcessDeletes();

		GameManager.ActiveScene = new Scene();

		Time.Now = msg.Time;

		if ( !string.IsNullOrWhiteSpace( msg.SceneData ) )
		{
			var sceneData = JsonObject.Parse( msg.SceneData ).AsObject();
			GameManager.ActiveScene.Deserialize( sceneData );
		}

		foreach ( var nwo in msg.NetworkObjects )
		{
			OnObjectCreate( nwo, null );
		}

		GameManager.IsPlaying = true;
	}

	public override void OnJoined( NetworkChannel client )
	{
		Log.Info( $"Client {client.Name} has joined the game!" );
	}

	public override void OnLeave( NetworkChannel client )
	{
		Log.Info( $"Client {client.Name} has left the game!" );

		GameManager.ActiveScene.DestroyNetworkObjects( x => x.Net.Owner == client.Id );

	}

	public override IDisposable Push()
	{
		return GameManager.ActiveScene.Push();
	}

	protected override void OnObjectCreate( in ObjectCreateMsg message, NetworkChannel source )
	{
		// TODO: Does source have the authority to create?

		var go = new GameObject();

		// TODO: Does this server allow this client to be creating objects from json?
		go.Deserialize( JsonObject.Parse( message.JsonData ).AsObject() );

		var netObject = go.GetComponent<NetworkObject>();
		netObject.Creator = message.Creator;
		netObject.Owner = message.Owner;

		go.SetNetworkObject( netObject );

		//go.Receive( message.Update );
	}

	protected override void OnObjectUpdate( in ObjectUpdateMsg message, NetworkChannel source )
	{
		var obj = GameManager.ActiveScene.Directory.FindByGuid( message.Guid );
		if ( obj is null )
		{
			Log.Warning( $"ObjectUpdate: Unknown object {message.Guid}" );
			return;
		}

		// TODO: Does source have the authority to update?

		obj.Receive( message );
	}

	protected override void OnObjectDestroy( in ObjectDestroyMsg message, NetworkChannel source )
	{
		var obj = GameManager.ActiveScene.Directory.FindByGuid( message.Guid );
		if ( obj is null )
		{
			Log.Warning( $"ObjectDestroy: Unknown object {message.Guid}" );
			return;
		}

		// TODO: Does source have the authoruty to destroy?

		if ( obj.Net is not null )
		{
			obj.Net.OnNetworkDestroy();
		}
		else
		{
			obj.Destroy();
		}
	}
}

