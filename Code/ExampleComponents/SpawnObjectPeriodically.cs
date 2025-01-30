public class SpawnObjectPeriodically : Component
{
	[Property] public GameObject PrefabToSpawn { get; set; }
	[Property] public float SpawnIntervalSeconds { get; set; } = 1.0f;
	[Property] public float ObjectLifeTimeSeconds { get; set; } = -1.0f;

	private TimeSince _timeSinceLastSpawn = 0f;

	protected override async void OnUpdate()
	{
		if ( _timeSinceLastSpawn >= SpawnIntervalSeconds )
		{
			_timeSinceLastSpawn = 0;
			var go = PrefabToSpawn.Clone( WorldPosition, WorldRotation );

			await Task.DelaySeconds( ObjectLifeTimeSeconds );
			go.Destroy();
		}
	}
}
