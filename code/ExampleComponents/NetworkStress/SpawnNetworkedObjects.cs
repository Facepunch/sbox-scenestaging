public class SpawnNetworkedObjects : Component
{
	[Property] public GameObject PrefabToSpawn { get; set; }
	[Property] public bool ParentToThis { get; set; }
	[Property] public int AmountToSpawn { get; set; } = 3;
	
	protected override void OnStart()
	{
		if ( !IsProxy )
		{
			for ( var i = 0; i < AmountToSpawn; i++ )
			{
				var go = PrefabToSpawn.Clone( Transform.Position, Rotation.Identity );

				if ( ParentToThis )
					go.SetParent( GameObject );
			
				go.NetworkSpawn();
			}
		}
		
		base.OnStart();
	}
}
