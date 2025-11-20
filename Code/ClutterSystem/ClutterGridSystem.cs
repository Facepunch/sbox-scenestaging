using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Game object system that manages a spatial grid for clutter placement.
/// Components can register their clutter configuration, and the system handles the rest.
/// </summary>
public sealed class ClutterGridSystem : GameObjectSystem
{
	private List<ClutterLayer> Layers { get; set; } = [];

	public ClutterGridSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ClutterGridSystem.Update" );
	}

	public ClutterLayer Register( ClutterSettings settings, GameObject parentObject )
	{
		var layer = new ClutterLayer( settings, parentObject );
		Layers.Add( layer );
		return layer;
	}

	public void Unregister( ClutterLayer layer )
	{
		Layers.Remove( layer );
	}

	private Vector2Int WorldToTile( Vector3 worldPos, float tileSize )
	{
		return new Vector2Int(
			(int)MathF.Floor( worldPos.x / tileSize ),
			(int)MathF.Floor( worldPos.y / tileSize )
		);
	}

	private void OnUpdate()
	{
		if ( Layers.Count == 0 )
			return;

		var camera = Scene.IsEditor
			? Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "editor_camera" )?.Components.Get<CameraComponent>()
			: Scene.Camera;

		if ( camera == null )
			return;

		foreach ( var layer in Layers )
		{
			if ( !layer.IsActive )
				continue;

			layer.UpdateTiles( camera.WorldPosition, WorldToTile );
		}
	}
}
