
public sealed class NetworkSession : Component
{
	protected override void OnStart()
	{
		//
		// Create a lobby if we're not connected
		//
		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new() );
		}
	}

}
