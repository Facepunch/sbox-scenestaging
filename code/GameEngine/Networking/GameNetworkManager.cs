using Sandbox;

public sealed class GameNetworkManager : BaseComponent
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject SpawnPoint { get; set; }

	public override void OnStart()
	{
		var myPlayerObject = SceneUtility.Instantiate( PlayerPrefab, SpawnPoint.Transform.World );
		myPlayerObject.Enabled = true;

		var nameTag = myPlayerObject.GetComponent<NameTagPanel>( false, true );
		if ( nameTag is not null )
		{
			nameTag.Name = Game.UserName;
		}

		NetworkObject.Instantiate( myPlayerObject );
	}

	public override void Update()
	{
		
	}
}
