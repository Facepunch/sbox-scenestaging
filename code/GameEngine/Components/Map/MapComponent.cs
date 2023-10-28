using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[Title( "Map" )]
[Category( "World" )]
[Icon( "visibility", "red", "white" )]
public class MapComponent : BaseComponent, BaseComponent.ExecuteInEditor
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

[Title( "Map Objects" )]
[Category( "World" )]
[Tag( "development" )]
[Icon( "maps_home_work" )]
public class MapObjectComponent : BaseComponent
{
	List<SceneObject> objects = new List<SceneObject>();

	public Action RecreateMapObjects;

	internal void AddSceneObjects( IEnumerable<SceneObject> sceneObjects )
	{
		objects.AddRange( sceneObjects );
	}

	public override void OnEnabled()
	{
		RecreateMapObjects?.Invoke();

		if ( !objects.Any() )
		{
			GameObject.Name += $" (empty)";
		}
	}

	public override void OnDisabled()
	{
		foreach( var obj in objects )
		{
			obj.Delete();
		}

		objects.Clear();
	}
}



file class MapComponentMapLoader : SceneMapLoader
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
		go.Transform.Local = kv.Transform;

		//
		// ideal situation here is that we look at the entities and create them
		// via components. Like for a spotlight we create a SpotLightComponent.
		// but that's a lot of work right now so lets just do this crazy hack
		// to allow the created SceneObjects be viewed as gameobject compoonents
		// and be turned on and off.. but nothing else.
		//

		var c = go.AddComponent<MapObjectComponent>();

		c.RecreateMapObjects += () =>
		{
			SceneObjects.Clear();
			base.CreateObject( kv );

			if ( SceneObjects.Count > 0 )
			{
				c.AddSceneObjects( SceneObjects );
			}
		};

		go.SetParent( map.GameObject, true );

		//go.Name += " (unhandled)";
	}
}
