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

	private Dictionary<Vector2Int, ClutterTile> Tiles { get; } = [];
	private ClutterBatch Batch { get; set; }

	private const float TileHeight = 10000f;

	private int _lastSettingsHash;

	public ClutterLayer( ClutterSettings settings, GameObject parentObject )
	{
		Settings = settings;
		ParentObject = parentObject;
		_lastSettingsHash = settings.GetHashCode();

		// Create owned ClutterBatch
		Batch = new ClutterBatch( parentObject.Scene.SceneWorld );
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
	/// Returns a list of generation jobs for tiles that need to be populated.
	/// </summary>
	public List<ClutterGenerationJob> UpdateTiles( Vector3 center )
	{
		List<ClutterGenerationJob> jobs = [];

		if ( !Settings.IsValid )
			return jobs;

		Center = center;

		var centerTile = WorldToTile( Center );
		HashSet<Vector2Int> activeCoords = [];

		for ( int x = -Settings.TileRadius; x <= Settings.TileRadius; x++ )
		{
			for ( int y = -Settings.TileRadius; y <= Settings.TileRadius; y++ )
			{
				var coord = new Vector2Int( centerTile.x + x, centerTile.y + y );
				activeCoords.Add( coord );

				if ( !HasTile( coord ) )
				{
					var job = CreateTileJob( coord );
					if ( job != null )
						jobs.Add( job );
				}
			}
		}

		// Remove tiles that are no longer active
		var tilesToRemove = GetTileCoordinates()
			.Where( coord => !activeCoords.Contains( coord ) )
			.ToList();

		// Clean em up
		foreach ( var coord in tilesToRemove )
		{
			RemoveTile( coord );
		}

		return jobs;
	}

	private Vector2Int WorldToTile( Vector3 worldPos )
	{
		return new Vector2Int(
			(int)MathF.Floor( worldPos.x / Settings.TileSize ),
			(int)MathF.Floor( worldPos.y / Settings.TileSize )
		);
	}

	/// <summary>
	/// Creates a generation job for a tile at the specified coordinates.
	/// </summary>
	private ClutterGenerationJob CreateTileJob( Vector2Int coord )
	{
		var tile = GetOrCreateTile( coord );

		if ( tile.IsPopulated || !Settings.IsValid )
			return null;

		return ClutterGenerationJob.Tile(
			tile.Bounds,
			tile,
			Settings.RandomSeed,
			Settings.Isotope,
			ParentObject
		);
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
	/// Disposes this layer and cleans up resources.
	/// </summary>
	public void Dispose()
	{
		ClearAllTiles();
	}

	/// <summary>
	/// Gets all tile coordinates currently in this layer.
	/// </summary>
	public IEnumerable<Vector2Int> GetTileCoordinates() => Tiles.Keys;

	/// <summary>
	/// Checks if a tile exists at the specified coordinates.
	/// </summary>
	public bool HasTile( Vector2Int coord ) => Tiles.ContainsKey( coord );

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
