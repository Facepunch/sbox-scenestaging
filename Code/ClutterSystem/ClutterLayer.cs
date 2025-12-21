namespace Sandbox.Clutter;

public class ClutterLayer
{
	public ClutterSettings Settings { get; set; }
	public GameObject ParentObject { get; set; }
	public ClutterGridSystem GridSystem { get; set; }

	private Dictionary<Vector2Int, ClutterTile> Tiles { get; } = [];
	
	/// <summary>
	/// Batches organized by model, containing all instances across all tiles in this layer.
	/// </summary>
	private Dictionary<Model, ClutterBatch> _batches = [];
	
	private int _lastSettingsHash;
	private const float TileHeight = 50000f;
	
	/// <summary>
	/// Flag to indicate batches need to be rebuilt.
	/// </summary>
	private bool _batchesDirty = false;

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
					ParentObject,
					this // Pass layer reference
				) );
			}
		}

		// Remove out-of-range tiles
		var toRemove = Tiles.Keys.Where( coord => !activeCoords.Contains( coord ) ).ToList();
		if ( toRemove.Count > 0 )
		{
			foreach ( var coord in toRemove )
			{
				if ( Tiles.Remove( coord, out var tile ) )
				{
					// Remove from pending set first to prevent queue buildup
					GridSystem?.RemovePendingTile( tile );
					tile.Destroy();
				}
			}
			_batchesDirty = true;
		}

		// Rebuild batches if needed (only when no jobs are pending)
		if ( _batchesDirty && jobs.Count == 0 )
		{
			RebuildBatches();
		}

		return jobs;
	}

	/// <summary>
	/// Called when a tile has been populated with instances.
	/// Marks batches as dirty so they'll be rebuilt.
	/// </summary>
	public void OnTilePopulated( ClutterTile tile )
	{
		_batchesDirty = true;
	}

	/// <summary>
	/// Rebuilds all batches from scratch using all populated tiles.
	/// </summary>
	public void RebuildBatches()
	{
		// Clear existing batches
		foreach ( var batch in _batches.Values )
			batch.Delete();
		
		_batches.Clear();

		// Early exit if no tiles
		if ( Tiles.Count == 0 )
		{
			_batchesDirty = false;
			return;
		}

		// Group instances by model in a single pass
		var instancesByModel = Tiles.Values
			.Where( t => t.IsPopulated )
			.SelectMany( t => t.ModelInstances )
			.Where( i => i.Entry?.Model != null )
			.GroupBy( i => i.Entry.Model )
			.ToDictionary( g => g.Key, g => g.ToList() );

		// Create and finalize batches
		foreach ( var (model, instances) in instancesByModel )
		{
			var batch = new ClutterBatch( ParentObject.Scene.SceneWorld );
			
			foreach ( var instance in instances )
				batch.AddInstance( instance );
			
			batch.Finalize();
			_batches[model] = batch;
		}

		_batchesDirty = false;
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
			_batchesDirty = true;
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
				_batchesDirty = true;
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

		// Clear all batches
		foreach ( var batch in _batches.Values )
		{
			batch.Delete();
		}
		_batches.Clear();
		_batchesDirty = false;
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
