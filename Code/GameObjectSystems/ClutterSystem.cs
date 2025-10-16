namespace Sandbox;

/// <summary>
/// The clutter system holds a world grid which indexes every clutter instance into its own cell optimized for rendering.
/// It monitors terrain changes to update instances as needed. It is the main API to interact with clutter instances at runtime
/// </summary>
public sealed class ClutterSystem : GameObjectSystem<ClutterSystem>, Component.ExecuteInEditor, ISceneMetadata
{
	private readonly Scene _scene;
	private bool _hasInitialized = false;

	/// <summary>
	/// The size in world unit of a cell. Each cell is batched and instanced together.
	/// </summary>
	public int CellSize { get; private set; } = 2048;

	/// <summary>
	/// Number of cell loaded
	/// </summary>
	public int Count => WorldGrid.Count;

	/// <summary>
	/// 2D world grid that stores instances by cell
	/// </summary>
	private Dictionary<string, ClutterCell> WorldGrid = [];

	/// <summary>
	/// Bookkeeping list of terrains we are monitoring for changes
	/// </summary>
	private List<Terrain> MoniteredTerrains = [];

	/// <summary>
	/// Queue of scatter requests to process next frame
	/// </summary>
	private Queue<Action> ScatterRequests = [];

	/// <summary>
	/// Maps VolumeId to the list of instances created by that volume.
	/// This is rebuilt on scene load from the CompressedInstances data.
	/// </summary>
	private Dictionary<Guid, List<ClutterInstance>> VolumeInstances = [];

	public ClutterSystem( Scene scene ) : base( scene )
	{
		_scene = scene;

		// Listen to scene events for initialization and updates
		Listen( Stage.SceneLoaded, 0, OnSceneLoaded, "ClutterSystemOnSceneLoaded");
		Listen( Stage.FinishUpdate, 0, OnUpdateFinished, "ClutterSystemUpdate" );
	}

	private void OnSceneLoaded()
	{
		if ( !_hasInitialized )
		{
			_hasInitialized = true;
			RebuildClutterMapping();
			SubscribeToTerrains();
		}
	}

	private void OnUpdateFinished()
	{
		// Process one scatter request per frame to avoid hitches
		if ( ScatterRequests.Count > 0 )
		{
			var action = ScatterRequests.Dequeue();
			action?.Invoke();
		}
	}

	private void RebuildClutterMapping()
	{
		RebuildGridFromExistingInstances();
		RebuildVolumeInstanceLists();
	}

	/// <summary>
	/// Rebuilds the spatial grid by doing BBox traces to find objects with clutter tags/components
	/// This is called when the system starts up to restore the grid from serialized data
	/// </summary>
	private void RebuildGridFromExistingInstances()
	{
		WorldGrid.Clear();

		int registeredCount = 0;

		// Register all clutter instances with the grid
		var clutterComponents = _scene.GetAllComponents<ClutterComponent>();
		foreach ( var clutterComponent in clutterComponents )
		{
			// Register all instances with the grid
			foreach ( var layer in clutterComponent.Layers )
			{
				foreach ( var instance in layer.Instances )
				{
					RegisterClutter( instance );
					registeredCount++;
				}
			}
		}

		Log.Info( $"ClutterSystem rebuilt grid with {Count} cells containing {registeredCount} instances" );
	}

	/// <summary>
	/// Rebuilds the instance lists for all ClutterVolumeComponents after deserialization.
	/// Maps compressed instances back to their originating volumes based on component Id.
	/// </summary>
	private void RebuildVolumeInstanceLists()
	{
		VolumeInstances.Clear();

		// Let's build a map of all clutter instances sorted by volume
		var volumes = _scene.GetAllComponents<ClutterVolumeComponent>();
		var volumeIds = new HashSet<Guid>( volumes.Select( v => v.Id ) );
		foreach ( var volume in volumes )
		{
			VolumeInstances[volume.Id] = [];
		}

		// Let's iterate over all clutter instances and add them to their volume association map.
		int totalRegistered = 0;

		var clutterComponents = _scene.GetAllComponents<ClutterComponent>();
		foreach ( var clutterComponent in clutterComponents )
		{
			// Get instance-to-volume mapping from serialized data
			var instanceVolumeMapping = ClutterSerializer.GetInstanceVolumeMapping( clutterComponent );

			foreach ( var layer in clutterComponent.Layers )
			{
				// Find instances that belong to volumes and register them
				foreach ( var instance in layer.Instances )
				{
					// Skip if not a model instance
					if ( instance.ClutterType != ClutterInstance.Type.Model )
						continue;

					// Check if this instance has a volume mapping
					if ( !instanceVolumeMapping.TryGetValue( instance.transform.Position, out var volumeId ) )
						continue;

					// Check if this volume still exists
					if ( !volumeIds.Contains( volumeId ) )
						continue;

					VolumeInstances[volumeId].Add( instance );
					totalRegistered++;
				}
			}
		}

		Log.Info( $"Rebuilt volume instance lists: {totalRegistered} instances mapped to {VolumeInstances.Count} volumes" );
	}

	/// <summary>
	/// Subscribe to all terrain modification events in the scene
	/// </summary>
	private void SubscribeToTerrains()
	{
		foreach ( var terrain in _scene.GetAllComponents<Terrain>() )
		{
			// Avoid subscribing multiple time
			if ( !MoniteredTerrains.Contains( terrain ) )
			{
				terrain.OnTerrainModified += ( flags, region ) => OnTerrainModified( terrain, flags, region );
				MoniteredTerrains.Add( terrain );
			}
		}
	}

	/// <summary>
	/// Triggered when a terrain has been modified. We can reposition the clutter to follow the terrain
	/// </summary>
	private void OnTerrainModified( Terrain terrain, Terrain.SyncFlags flags, RectInt region )
	{
		// Only respond to height changes
		if ( !flags.HasFlag( Terrain.SyncFlags.Height ) )
			return;

		if ( terrain?.Storage == null )
			return;

		// Convert terrain region to world space to find affected cells
		var terrainSize = terrain.TerrainSize;
		var terrainResolution = terrain.Storage.Resolution;
		var pixelToWorld = terrainSize / terrainResolution;

		var worldMinX = region.Left * pixelToWorld;
		var worldMinY = region.Top * pixelToWorld;
		var worldMaxX = region.Right * pixelToWorld;
		var worldMaxY = region.Bottom * pixelToWorld;

		var terrainBounds = new BBox(
			new Vector2( worldMinX, worldMinY ),
			new Vector2( worldMaxX, worldMaxY )
		);

		var (minCellX, minCellY, maxCellX, maxCellY) = GetCellRangeFromBounds( terrainBounds );

		// Rebuild all affected cells with height updates
		for ( int x = minCellX; x <= maxCellX; x++ )
		{
			for ( int y = minCellY; y <= maxCellY; y++ )
			{
				var cell = GetCell( new( x, y ), false );
				cell?.OnTerrainModified();
			}
		}
	}

	/// <summary>
	/// Gets the list of instances associated with a specific volume
	/// </summary>
	public List<ClutterInstance> GetVolumeInstances( Guid volumeId )
	{
		if ( VolumeInstances.TryGetValue( volumeId, out var instances ) )
		{
			return [.. instances];
		}
		return [];
	}

	/// <summary>
	/// Registers instances as belonging to a specific volume
	/// </summary>
	public void RegisterVolumeInstances( Guid volumeId, List<ClutterInstance> instances )
	{
		if ( !VolumeInstances.TryGetValue( volumeId, out List<ClutterInstance> value ) )
		{
			value = [];
			VolumeInstances[volumeId] = value;
		}

		value.AddRange( instances );
	}

	/// <summary>
	/// Clears all instances associated with a specific volume.
	/// </summary>
	public void ClearVolume( ClutterVolumeComponent volume )
	{
		var volumeId = volume.Id;

		// Get instances from volume map
		var volumeInstances = GetVolumeInstances( volumeId );
		if ( volumeInstances.Count == 0 )
			return;

		// Collect all
		HashSet<Guid> myInstanceIds = [];
		foreach ( var instance in volumeInstances )
		{
			myInstanceIds.Add( instance.InstanceId );
		}

		// Unregister all instances from grid
		UnregisterClutters( volumeInstances );

		// Destroy and remove from layers
		foreach ( var instance in volumeInstances )
		{
			ClutterComponent.DestroyInstance( instance );
		}

		// Clear runtime instances from ALL layers in all ClutterComponents
		var clutterComponents = Scene.GetAllComponents<ClutterComponent>();
		foreach ( var component in clutterComponents )
		{
			foreach ( var layer in component.Layers )
			{
				layer.Instances.RemoveAll( instance => myInstanceIds.Contains( instance.InstanceId ) );
			}

			// Serialize the changes to the component
			component.SerializeData();
		}

		// Clear tracking data
		if ( VolumeInstances.TryGetValue( volumeId, out List<ClutterInstance> value ) )
		{
			value.Clear();
		}

		// Also clear any child GameObjects with "scattered"
		var childrenToDestroy = new List<GameObject>();
		CollectScatteredChildren( volume.GameObject, childrenToDestroy );

		foreach ( var child in childrenToDestroy )
		{
			var clutterInstance = new ClutterInstance( child, child.WorldTransform );
			UnregisterClutter( clutterInstance );
			child.Destroy();
		}

		Log.Info( $"Cleared {volumeInstances.Count} scattered objects from volume '{volume.GameObject.Name}'" );
	}

	/// <summary>
	/// Registers a clutter instance into the grid. It will be taken into account when making a query
	/// </summary>
	public void RegisterClutter( ClutterInstance obj )
	{
		// Skip instances with near-zero scale
		if ( obj.transform.Scale.Length < 0.01f )
			return;

		var cellPosition = GetCellPositionFromWorld( obj.transform.Position );
		var cell = GetCell( cellPosition );
		cell.AddInstance( obj );
		cell.RefreshDatabase();
	}

	/// <summary>
	/// Registers multiple clutter instances into the grid.
	/// </summary>
	public void RegisterClutters( Span<ClutterInstance> instances )
	{
		// Keep track of which cell was modified so we can refresh them
		HashSet<ClutterCell> modifiedCells = [];
		for ( int i = 0; i < instances.Length; i++ )
		{
			// Skip instances with near-zero scale
			if ( instances[i].transform.Scale.Length < 0.01f )
				continue;

			var cellPosition = GetCellPositionFromWorld( instances[i].transform.Position );
			var cell = GetCell( cellPosition );
			cell.AddInstance( instances[i] );
			modifiedCells.Add( cell );
		}

		// Refresh all cells to rebuild render batches
		foreach ( ClutterCell cell in modifiedCells )
		{
			cell.RefreshDatabase();
		}
	}

	/// <summary>
	/// Unregisters a clutter instance from the grid
	/// </summary>
	public void UnregisterClutter( ClutterInstance obj )
	{
		var cellPosition = GetCellPositionFromWorld( obj.transform.Position );
		var cell = GetCell( cellPosition, false );
		if ( cell == null ) return;

		cell.RemoveInstance( obj );

		// Delete cell if it's now empty
		if ( cell.IsEmpty() )
		{
			WorldGrid.Remove( Hash( cellPosition ) );
			cell.Delete();
		}

		cell.RefreshDatabase();
	}

	/// <summary>
	/// Unregisters multiple clutter instances from the grid and rebuilds affected cells
	/// </summary>
	public void UnregisterClutters( List<ClutterInstance> instances )
	{
		if ( instances.Count == 0 )
			return;

		Dictionary<ClutterCell, List<ClutterInstance>> cellToInstances = [];
		List<ClutterCell> cellsToDelete = [];

		foreach ( var instance in instances )
		{
			var cellPosition = GetCellPositionFromWorld( instance.transform.Position );
			var cell = GetCell( cellPosition, createIfMissing: false );

			if ( cell == null ) // cell could be null since CreateIfMissing is false...
				continue;

			if ( !cellToInstances.TryGetValue( cell, out var list ) )
			{
				list = [];
				cellToInstances[cell] = list;
			}

			list.Add( instance );
		}

		// Remove from each cell and rebuild - O(C) where C = affected cells
		foreach ( var (cell, cellInstances) in cellToInstances )
		{
			cell.RemoveInstances( cellInstances );
			cell.RefreshDatabase();

			// Check if cell is now empty and mark for deletion
			if ( cell.IsEmpty() )
			{
				cellsToDelete.Add( cell );
			}
		}

		// Delete empty cells
		foreach ( var cell in cellsToDelete )
		{
			var cellPosition = GetCellPositionFromWorld( cell.GetCellBounds().Center );
			var hash = Hash( cellPosition );
			WorldGrid.Remove( hash );
			cell.Delete();
		}

		Log.Info( $"Unregistered {instances.Count} instances from {cellToInstances.Count} cells ({cellsToDelete.Count} cells deleted)" );
	}

	/// <summary>
	/// Gets the cells that overlap with the given bounds
	/// </summary>
	public List<ClutterCell> GetOverlappingCells( BBox bounds, bool createIfMissing = true )
	{
		var cells = new List<ClutterCell>();

		// Calculate cell range from bounds
		int minX = (int)MathF.Floor( bounds.Mins.x / CellSize );
		int minY = (int)MathF.Floor( bounds.Mins.y / CellSize );
		int maxX = (int)MathF.Floor( bounds.Maxs.x / CellSize );
		int maxY = (int)MathF.Floor( bounds.Maxs.y / CellSize );

		// Get cells in this range
		for ( int x = minX; x <= maxX; x++ )
		{
			for ( int y = minY; y <= maxY; y++ )
			{
				var cellPos = new Vector2( x, y );
				var cell = GetCell( cellPos, createIfMissing );
				if ( cell != null )
				{
					cells.Add( cell );
				}
			}
		}

		return cells;
	}

	/// <summary>
	/// Queues a scatter request to be processed next frame
	/// </summary>
	public void QueueScatterRequest( Action scatterAction )
	{
		ScatterRequests.Enqueue( scatterAction );
	}

	/// <summary>
	/// Returns the cell size coordinates given a world position.
	/// </summary>
	internal Vector2 GetCellPositionFromWorld( Vector3 position )
	{
		int x = (int)MathF.Floor( position.x / CellSize );
		int y = (int)MathF.Floor( position.y / CellSize );
		return new( x, y );
	}

	/// <summary>
	/// Gets the cell coordinate range that overlaps with the given world bounds
	/// </summary>
	private (int minX, int minY, int maxX, int maxY) GetCellRangeFromBounds( BBox bounds )
	{
		int minX = (int)MathF.Floor( bounds.Mins.x / CellSize );
		int minY = (int)MathF.Floor( bounds.Mins.y / CellSize );
		int maxX = (int)MathF.Floor( bounds.Maxs.x / CellSize );
		int maxY = (int)MathF.Floor( bounds.Maxs.y / CellSize );
		return (minX, minY, maxX, maxY);
	}

	/// <summary>
	/// Returns a cell at the given cell position. Creates it if it doesn't exist and createIfMissing is true.
	/// </summary>
	internal ClutterCell? GetCell( Vector2 cellPosition, bool createIfMissing = true )
	{
		var hash = Hash( cellPosition );
		if ( !WorldGrid.ContainsKey( hash ) )
		{
			if ( !createIfMissing ) return null;
			WorldGrid[hash] = new ClutterCell( _scene, cellPosition, CellSize );
		}

		return WorldGrid[hash];
	}

	/// <summary>
	/// Recursively collects GameObject children with the "scattered" tag
	/// </summary>
	private void CollectScatteredChildren( GameObject parent, List<GameObject> result )
	{
		foreach ( var child in parent.Children )
		{
			if ( child.Tags.Has( "scattered" ) )
			{
				result.Add( child );
			}
			else
			{
				CollectScatteredChildren( child, result );
			}
		}
	}

	private static string Hash( Vector2 position )
	{
		return position.GetHashCode().ToString();
	}

	public Dictionary<string, string> GetMetadata()
	{
		return new()
		{
			{ "testing", "test" },
			{ "ClutterSystem_CellCount", Count.ToString() },
			{ "ClutterSystem_VolumeCount", VolumeInstances.Count.ToString() }
		};
	}
}
