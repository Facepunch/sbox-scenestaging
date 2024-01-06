using Sandbox;

public sealed class MapLoadedHandler : Component
{
	[Property] public MapInstance MapInstance { get; set; }
	[Property] public GameObject PlayerObject { get; set; }

	protected override void OnAwake()
	{
		base.OnAwake();
	}

	protected override void OnEnabled()
	{
		if ( MapInstance is not null )
		{
			MapInstance.OnMapLoaded = OnMapLoaded;

			if ( MapInstance.IsLoaded )
			{
				OnMapLoaded();
			}
		}
	}

	protected override void OnDisabled()
	{
		if ( MapInstance is not null )
		{
			MapInstance.OnMapLoaded -= OnMapLoaded;
		}
	}

	void OnMapLoaded()
	{
		Log.Info( "Map has been loaded!" );

		var spawnPoints = Scene.Directory.FindByName( "info_player_start" ).ToArray();
		var randomSpawn = Random.Shared.FromArray( spawnPoints );
		if ( randomSpawn  is not null )
		{
			PlayerObject.Transform.Position = randomSpawn.Transform.Position + Vector3.Up * 64;
			PlayerObject.Transform.Rotation = randomSpawn.Transform.Rotation;
		}
	}

}
