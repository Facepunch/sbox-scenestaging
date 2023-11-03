using Sandbox;
using Sandbox.Network;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// This is created and referenced by the network system, as a way to route.
/// </summary>
public class SceneNetworkSystem : GameNetworkSystem
{
	static Guid NetworkGuid = Guid.NewGuid();
	internal static SceneNetworkSystem Instance { get; private set; }

	public static Guid LocalGuid() => NetworkGuid;

	public SceneNetworkSystem( Sandbox.Internal.TypeLibrary library, NetworkSystem system ) : base( library, system )
	{
		Instance = this;
		Log.Info( "SceneNetworkSystem Initialized" );

		AddJsonHandler<Net_ObjectCreate>( NetworkObject.CreateFromWire );
	}

	/// <summary>
	/// A client has joined and wants a snapshot of the world
	/// </summary>
	public override async Task<JsonObject> GetSnapshotAsync( NetworkUser source )
	{
		ThreadSafe.AssertIsMainThread();

		return GameManager.ActiveScene.Serialize();
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

	public override async Task OnJoined( NetworkUser client )
	{
		Log.Info( $"Client {client.Name} has joined the game!" );
	}

	public override async Task OnLeave( NetworkUser client )
	{
		Log.Info( $"Client {client.Name} has left the game!" );
	}
}


public struct Net_ObjectCreate
{
	public JsonObject JsonData { get; set; }
	public Guid Guid { get; set; }
	public Guid Creator { get; set; }
	public Guid Owner { get; set; }
	
}
