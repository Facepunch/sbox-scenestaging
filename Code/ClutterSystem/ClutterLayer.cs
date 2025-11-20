using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Represents a layer of clutter with its own settings and tiles.
/// Each layer independently manages its tile generation and lifecycle.
/// </summary>
public class ClutterLayer
{
	public ClutterSettings Settings { get; set; }
	public Vector3 Center { get; set; }
	public bool IsActive { get; set; } = true;
	public GameObject ParentObject { get; set; }
	
	private Dictionary<Vector2Int, ClutterTile> Tiles { get; } = new();
	private const float TileHeight = 10000f;
	private int _lastSettingsHash;

	public ClutterLayer( ClutterSettings settings, GameObject parentObject )
	{
		Settings = settings;
		ParentObject = parentObject;
		_lastSettingsHash = settings.GetHashCode();
	}

	/// <summary>
	/// Updates the layer with new settings, regenerating tiles if needed.
	/// </summary>
	public void UpdateSettings( ClutterSettings newSettings )
	{
		var newHash = newSettings.GetHashCode();
		if ( newHash != _lastSettingsHash )
		{
			ClearAllTiles();
			Settings = newSettings;
			_lastSettingsHash = newHash;
		}
	}

	/// <summary>
	/// Updates tiles around a center point (usually camera position).
	/// </summary>
	public void UpdateTiles( Vector3 center, System.Func<Vector3, float, Vector2Int> worldToTile )
	{
		if ( !Settings.IsValid )
			return;

		Center = center;

		var centerTile = worldToTile( Center, Settings.TileSize );
		var activeCoords = new HashSet<Vector2Int>();

		for ( int x = -Settings.TileRadius; x <= Settings.TileRadius; x++ )
		{
			for ( int y = -Settings.TileRadius; y <= Settings.TileRadius; y++ )
			{
				var coord = new Vector2Int( centerTile.x + x, centerTile.y + y );
				activeCoords.Add( coord );

				if ( !HasTile( coord ) )
				{
					PopulateTile( coord );
				}
			}
		}

		var tilesToRemove = GetTileCoordinates()
			.Where( coord => !activeCoords.Contains( coord ) )
			.ToList();

		foreach ( var coord in tilesToRemove )
		{
			RemoveTile( coord );
		}
	}

	/// <summary>
	/// Gets a tile at the specified coordinates, creating it if needed.
	/// </summary>
	public ClutterTile GetOrCreateTile( Vector2Int coord )
	{
		if ( !Tiles.TryGetValue( coord, out var tile ) )
		{
			tile = new ClutterTile
			{
				Coordinates = coord,
				Bounds = GetTileBounds( coord ),
				SeedOffset = Settings.RandomSeed
			};
			
			Tiles[coord] = tile;
		}

		return tile;
	}

	/// <summary>
	/// Removes a tile at the specified coordinates.
	/// </summary>
	public void RemoveTile( Vector2Int coord )
	{
		if ( Tiles.TryGetValue( coord, out var tile ) )
		{
			tile.Destroy();
			Tiles.Remove( coord );
		}
	}

	/// <summary>
	/// Clears all tiles from this layer.
	/// </summary>
	public void ClearAllTiles()
	{
		foreach ( var tile in Tiles.Values )
		{
			tile.Destroy();
		}
		Tiles.Clear();
	}

	/// <summary>
	/// Gets all tile coordinates currently in this layer.
	/// </summary>
	public IEnumerable<Vector2Int> GetTileCoordinates() => Tiles.Keys;

	/// <summary>
	/// Checks if a tile exists at the specified coordinates.
	/// </summary>
	public bool HasTile( Vector2Int coord ) => Tiles.ContainsKey( coord );

	/// <summary>
	/// Populates a tile with scattered objects.
	/// </summary>
	private void PopulateTile( Vector2Int coord )
	{
		var tile = GetOrCreateTile( coord );
		
		if ( tile.IsPopulated )
			return;

		if ( !Settings.IsValid )
			return;

		Settings.Scatterer.Scatter( tile.Bounds, Settings.Isotope, tile, ParentObject );
		tile.IsPopulated = true;
	}

	private BBox GetTileBounds( Vector2Int coord )
	{
		var min = new Vector3(
			coord.x * Settings.TileSize,
			coord.y * Settings.TileSize,
			-TileHeight
		);

		var max = new Vector3(
			(coord.x + 1) * Settings.TileSize,
			(coord.y + 1) * Settings.TileSize,
			TileHeight
		);

		return new BBox( min, max );
	}
}
