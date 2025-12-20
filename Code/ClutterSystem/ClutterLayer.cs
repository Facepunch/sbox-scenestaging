using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

public class ClutterLayer
{
	public ClutterSettings Settings { get; set; }
	public GameObject ParentObject { get; set; }
	public ClutterGridSystem GridSystem { get; set; }

	private Dictionary<Vector2Int, ClutterTile> Tiles { get; } = [];
	private int _lastSettingsHash;
	private const float TileHeight = 50000f;

	public ClutterLayer( ClutterSettings settings, GameObject parentObject, ClutterGridSystem gridSystem )
	{
		Settings = settings;
		ParentObject = parentObject;
		GridSystem = gridSystem;
		_lastSettingsHash = settings.GetHashCode();
	}

	public void UpdateSettings( ClutterSettings newSettings )
	{
		var newHash = newSettings.GetHashCode();
		if ( newHash == _lastSettingsHash )
			return;

		ClearAllTiles();
		Settings = newSettings;
		_lastSettingsHash = newHash;
	}

	public List<ClutterGenerationJob> UpdateTiles( Vector3 center )
	{
		if ( !Settings.IsValid )
			return [];

		var centerTile = WorldToTile( center );
		var activeCoords = new HashSet<Vector2Int>();
		var jobs = new List<ClutterGenerationJob>();

		// Find tiles that should exist
		for ( int x = -Settings.Clutter.TileRadius; x <= Settings.Clutter.TileRadius; x++ )
		for ( int y = -Settings.Clutter.TileRadius; y <= Settings.Clutter.TileRadius; y++ )
		{
			var coord = new Vector2Int( centerTile.x + x, centerTile.y + y );
			activeCoords.Add( coord );

			// Get or create tile
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

			// Queue job if not populated
			if ( !tile.IsPopulated )
			{
				jobs.Add( ClutterGenerationJob.Tile(
					tile.Bounds,
					tile,
					Settings.RandomSeed,
					Settings.Clutter,
					ParentObject
				) );
			}
		}

		// Remove out-of-range tiles IMMEDIATELY
		var toRemove = Tiles.Keys.Where( coord => !activeCoords.Contains( coord ) ).ToList();
		foreach ( var coord in toRemove )
		{
			if ( Tiles.Remove( coord, out var tile ) )
			{
				// Remove from pending set first to prevent queue buildup
				GridSystem?.RemovePendingTile( tile );
				tile.Destroy();
			}
		}

		return jobs;
	}

	/// <summary>
	/// Invalidates the tile at the given world position, causing it to regenerate.
	/// </summary>
	public void InvalidateTile( Vector3 worldPosition )
	{
		var coord = WorldToTile( worldPosition );
		if ( Tiles.TryGetValue( coord, out var tile ) )
		{
			GridSystem?.RemovePendingTile( tile );
			tile.Destroy();
		}
	}

	/// <summary>
	/// Invalidates all tiles that intersect the given bounds, causing them to regenerate.
	/// </summary>
	public void InvalidateTilesInBounds( BBox bounds )
	{
		var minTile = WorldToTile( bounds.Mins );
		var maxTile = WorldToTile( bounds.Maxs );

		for ( int x = minTile.x; x <= maxTile.x; x++ )
		for ( int y = minTile.y; y <= maxTile.y; y++ )
		{
			var coord = new Vector2Int( x, y );
			if ( Tiles.TryGetValue( coord, out var tile ) )
			{
				GridSystem?.RemovePendingTile( tile );
				tile.Destroy();
			}
		}
	}

	public void ClearAllTiles()
	{
		foreach ( var tile in Tiles.Values )
		{
			GridSystem?.RemovePendingTile( tile );
			tile.Destroy();
		}

		Tiles.Clear();
	}

	private Vector2Int WorldToTile( Vector3 worldPos ) => new(
		(int)MathF.Floor( worldPos.x / Settings.Clutter.TileSize ),
		(int)MathF.Floor( worldPos.y / Settings.Clutter.TileSize )
	);

	private BBox GetTileBounds( Vector2Int coord ) => new(
		new Vector3( coord.x * Settings.Clutter.TileSize, coord.y * Settings.Clutter.TileSize, -TileHeight ),
		new Vector3( (coord.x + 1) * Settings.Clutter.TileSize, (coord.y + 1) * Settings.Clutter.TileSize, TileHeight )
	);
}
