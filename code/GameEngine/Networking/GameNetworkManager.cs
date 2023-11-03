using Sandbox;

public sealed class GameNetworkManager : BaseComponent
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject SpawnPoint { get; set; }

	public override void OnStart()
	{
		var myPlayerObject = SceneUtility.Instantiate( PlayerPrefab, SpawnPoint.Transform.World );
		myPlayerObject.Enabled = true;

		NetworkObject.Instantiate( myPlayerObject );
	}

	public override void Update()
	{
		
	}
}
