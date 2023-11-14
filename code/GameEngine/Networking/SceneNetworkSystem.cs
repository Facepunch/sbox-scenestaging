using Sandbox;
using Sandbox.Network;
using Sandbox.Utility;
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

		AddJsonHandler<Net_ObjectCreate>( NetworkObject.CreateFromWire );
		AddJsonHandler<Net_ObjectUpdate>( GameObject.ObjectUpdate );
	}

	/// <summary>
	/// A client has joined and wants a snapshot of the world
	/// </summary>
	public override async Task<JsonObject> GetSnapshotAsync( NetworkChannel source )
	{
		ThreadSafe.AssertIsMainThread();

		var o = new GameObject.SerializeOptions();
		o.SceneForNetwork = true;

		var s = GameManager.ActiveScene.Serialize( o );

		Log.Info( s.ToJsonString() );

		return s;
	}

	/// <summary>
	/// We have recieved a snapshot of the world
	/// </summary>
	public override async Task SetSnapshotAsync( JsonObject data )
	{
		ThreadSafe.AssertIsMainThread();

		// TODO - really shouldn't allow set and get of ActiveScene
		// we should have a switchscene function, that disables everything
		// in the old scene.

		GameManager.ActiveScene?.Clear();
		GameManager.ActiveScene?.Destroy();
		GameManager.ActiveScene?.ProcessDeletes();

		GameManager.ActiveScene = new Scene();
		GameManager.ActiveScene.Deserialize( data );

		GameManager.IsPlaying = true;
	}

	public override void OnJoined( NetworkChannel client )
	{
		Log.Info( $"Client {client.Name} has joined the game!" );
	}

	public override void OnLeave( NetworkChannel client )
	{
		Log.Info( $"Client {client.Name} has left the game!" );
	}

	public override IDisposable Push()
	{
		return GameManager.ActiveScene.Push();
	}
}


public struct Net_ObjectCreate
{
	public JsonObject JsonData { get; set; }
	public Guid Guid { get; set; }
	public Guid Creator { get; set; }
	public Guid Owner { get; set; }
	
}

public struct Net_ObjectUpdate
{
	public Guid Guid { get; set; }
	public Transform Transform { get; set; }
	public Guid Parent { get; set; }

}
