using Sandbox;
using Sandbox.Network;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

/// <summary>
/// This is created and referenced by the network system, as a way to route.
/// </summary>
public class SceneNetworkSystem : GameNetworkSystem
{
	public SceneNetworkSystem( NetworkSystem system ) : base( system )
	{
		Log.Info( "SceneNetworkSystem Initialized" );
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
}
