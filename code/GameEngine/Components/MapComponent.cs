using Sandbox;
using Sandbox.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[Title( "Map" )]
[Category( "World" )]
[Icon( "visibility", "red", "white" )]
public class MapComponent : GameObjectComponent
{
	[Property]
	public string MapName { get; set; }

	Map loadedMap;

	Task loadingTask;

	public override void OnEnabled()
	{
		base.OnEnabled();

		loadingTask = OnEnabledAsync();
	}

	public override void OnDisabled()
	{
		loadedMap?.Delete();
		loadedMap = null;

		foreach( var child in GameObject.Children )
		{
			child.Destroy();
		}
	}

	async Task OnEnabledAsync()
	{
		var package = await Package.Fetch( MapName, false );

		if ( package is null )
			return;

		await package.MountAsync();

		var assetName = package.GetMeta<string>( "PrimaryAsset" );

		loadedMap = new Map( assetName, new MapComponentMapLoader( this ) );

		foreach( var body in loadedMap.PhysicsGroup.Bodies )
		{
			var go = new GameObject();
			go.Flags |= GameObjectFlags.NotSaved;
			go.Name = "World Physics";
			var co = go.AddComponent<ColliderMapComponent>();
			co.SetBody( body );
			go.SetParent( GameObject, true );
		}
	}

}


file class MapComponentMapLoader : MapLoader
{
	MapComponent map;

	public MapComponentMapLoader( MapComponent mapComponent ) : base( mapComponent.Scene.SceneWorld, mapComponent.Scene.PhysicsWorld )
	{
		map = mapComponent;
	}

	protected override void CreateObject( ObjectEntry kv )
	{
		var go = new GameObject();
		go.Flags |= GameObjectFlags.NotSaved;
		go.Name = $"{kv.TypeName}";
		go.WorldTransform = kv.Transform;
		go.SetParent( map.GameObject, true );


		go.Name += " (unhandled)";
	}
}
