using Sandbox;
using Sandbox.Network;
using System.Threading.Tasks;

public sealed class NetworkSession : BaseComponent
{
	protected override void OnStart()
	{
		//
		// Create a lobby if we're not connected
		//
		if ( SceneNetworkSystem.Instance is null )
		{
			GameNetworkSystem.CreateLobby();
		}
	}

}
