using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Game object system that manages a spatial grid for clutter placement.
/// Components can register their clutter configuration, and the system handles the rest.
/// </summary>
public sealed class ClutterGridSystem : GameObjectSystem
{
	/// <summary>
	/// Data registered by a component for clutter generation.
	/// </summary>
	public class ClutterData
	{
		public float TileSize { get; set; } = 512f;
		public int TileRadius { get; set; } = 4;
		public ClutterIsotope Isotope { get; set; }
		public Scatterer Scatterer { get; set; }
		public Vector3 Center { get; set; }
		public bool IsActive { get; set; } = true;
		public GameObject ParentObject { get; set; }
		
		/// <summary>
		/// Tiles owned by this specific registration.
		/// Internal - managed by ClutterGridSystem.
		/// </summary>
		internal Dictionary<Vector2Int, ClutterTile> Tiles { get; } = new();
	}

	/// <summary>
	/// All registered clutter data entries.
	/// </summary>
	private List<ClutterData> RegisteredData { get; set; } = new();

	public ClutterGridSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ClutterGridSystem.Update" );
	}

	/// <summary>
	/// Registers clutter data with the system.
	/// </summary>
	/// <returns>The registered data object that can be modified or unregistered later</returns>
	public ClutterData Register( ClutterIsotope isotope, Scatterer scatterer, GameObject parentObject, float tileSize = 512f, int tileRadius = 4 )
	{
		var data = new ClutterData
		{
			Isotope = isotope,
			Scatterer = scatterer,
			ParentObject = parentObject,
			TileSize = tileSize,
			TileRadius = tileRadius
		};

		RegisteredData.Add( data );
		return data;
	}

	/// <summary>
	/// Unregisters clutter data from the system.
	/// </summary>
	public void Unregister( ClutterData data )
	{
		RegisteredData.Remove( data );
	}

	/// <summary>
	/// Gets the tile coordinates for a world position and tile size.
	/// </summary>
	public Vector2Int WorldToTile( Vector3 worldPos, float tileSize )
	{
		return new Vector2Int(
			(int)MathF.Floor( worldPos.x / tileSize ),
			(int)MathF.Floor( worldPos.y / tileSize )
		);
	}

	/// <summary>
	/// Gets the world-space bounds for a tile coordinate.
	/// </summary>
	public BBox GetTileBounds( Vector2Int tileCoord, float tileSize, Vector3 center )
	{
		var min = new Vector3(
			tileCoord.x * tileSize,
			tileCoord.y * tileSize,
			center.z - 1000f
		);

		var max = new Vector3(
			(tileCoord.x + 1) * tileSize,
			(tileCoord.y + 1) * tileSize,
			center.z + 1000f
		);

		return new BBox( min, max );
	}

	/// <summary>
	/// Gets or creates a tile at the specified coordinates.
	/// </summary>
	private ClutterTile GetOrCreateTile( Vector2Int coord, ClutterData data )
	{
		if ( !data.Tiles.TryGetValue( coord, out var tile ) )
		{
			tile = new ClutterTile
			{
				Coordinates = coord,
				Bounds = GetTileBounds( coord, data.TileSize, data.Center )
			};
			data.Tiles[coord] = tile;
		}

		return tile;
	}

	/// <summary>
	/// Removes a tile at the specified coordinates for a specific registration.
	/// Destroys all objects spawned in that tile.
	/// </summary>
	private void RemoveTile( Vector2Int coord, ClutterData data )
	{
		if ( data.Tiles.TryGetValue( coord, out var tile ) )
		{
			tile.Destroy();
			data.Tiles.Remove( coord );
		}
	}

	/// <summary>
	/// Clears all tiles from all registrations.
	/// Destroys all spawned clutter objects.
	/// </summary>
	public void ClearAllTiles()
	{
		foreach ( var data in RegisteredData )
		{
			foreach ( var tile in data.Tiles.Values )
			{
				tile.Destroy();
			}
			data.Tiles.Clear();
		}
	}

	/// <summary>
	/// Populates a tile with scattered objects.
	/// </summary>
	private void PopulateTile( Vector2Int coord, ClutterData data )
	{
		var tile = GetOrCreateTile( coord, data );
		
		if ( tile.IsPopulated )
			return;

		if ( data.Isotope == null || data.Scatterer == null )
			return;

		data.Scatterer.Scatter( tile.Bounds, data.Isotope, tile, data.ParentObject );
		
		tile.IsPopulated = true;
	}

	private void OnUpdate()
	{
		if ( RegisteredData.Count == 0 )
			return;

		// Get camera - in editor mode, look for editor_camera GameObject
		CameraComponent camera = null;
		
		if ( Scene.IsEditor )
		{
			// In editor, find the editor_camera GameObject
			var editorCamGo = Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "editor_camera" );
			camera = editorCamGo?.Components.Get<CameraComponent>();
		}
		else
		{
			// At runtime, use Scene.Camera
			camera = Scene.Camera;
		}

		if ( camera == null )
			return;

		// Process each registered clutter data
		foreach ( var data in RegisteredData )
		{
			if ( !data.IsActive || data.Isotope == null || data.Scatterer == null )
				continue;

			// Update center (follow camera)
			data.Center = camera.WorldPosition;

			var centerTile = WorldToTile( data.Center, data.TileSize );

			// Track active tiles for this registration
			var activeCoordsForThisData = new HashSet<Vector2Int>();

			// Generate tiles around center
			for ( int x = -data.TileRadius; x <= data.TileRadius; x++ )
			{
				for ( int y = -data.TileRadius; y <= data.TileRadius; y++ )
				{
					var coord = new Vector2Int( centerTile.x + x, centerTile.y + y );
					activeCoordsForThisData.Add( coord );

					if ( !data.Tiles.ContainsKey( coord ) )
					{
						PopulateTile( coord, data );
					}
				}
			}

			// Remove inactive tiles for this registration
			var tilesToRemove = new List<Vector2Int>();
			foreach ( var coord in data.Tiles.Keys )
			{
				if ( !activeCoordsForThisData.Contains( coord ) )
				{
					tilesToRemove.Add( coord );
				}
			}

			foreach ( var coord in tilesToRemove )
			{
				RemoveTile( coord, data );
			}
		}
	}
}
