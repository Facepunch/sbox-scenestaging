public class SpawnMovingObjects : Component, Component.INetworkListener
{
	[Property] public GameObject PrefabToSpawn { get; set; }
	[Property] public int AmountToSpawn { get; set; } = 150;
	
	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			for ( var i = 0; i < AmountToSpawn; i++ )
			{
				var go = PrefabToSpawn.Clone( WorldPosition );
				go.NetworkSpawn();
			}
		}
		
		base.OnStart();
	}
}
