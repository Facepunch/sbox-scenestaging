using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Clutter;

/// <summary>
/// Game object system that manages clutter generation.
/// Handles infinite streaming layers and executes generation jobs.
/// </summary>
public sealed partial class ClutterGridSystem : GameObjectSystem
{
	/// <summary>
	/// Mapping of clutter components to their respective layers
	/// </summary>
	private Dictionary<ClutterComponent, ClutterLayer> ComponentToLayer { get; set; } = [];

	private const int MAX_JOBS_PER_FRAME = 8;
	private const int MAX_PENDING_JOBS = 100;

	private List<ClutterGenerationJob> PendingJobs { get; set; } = [];
	private HashSet<ClutterTile> PendingTiles { get; set; } = [];
	private HashSet<Terrain> SubscribedTerrains { get; set; } = [];

	private Vector3 LastCameraPosition { get; set; }

	[Property]
	public ClutterStorage _storage { get; set; } = new();

	private ClutterLayer _painted;
	private bool _dirty = false;

	public ClutterGridSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ClutterGridSystem.Update" );
		Listen( Stage.SceneLoaded, 0, RebuildPaintedLayer, "ClutterGridSystem.RestorePainted" );
	}

	/// <summary>
	/// Check for new terrains, queue generation/cleanup jobs, and process pending jobs.
	/// </summary>
	private void OnUpdate()
	{
		var camera = GetActiveCamera();
		if ( camera == null )
			return;

		LastCameraPosition = camera.WorldPosition;

		SubscribeToTerrains();
		UpdateInfiniteLayers( LastCameraPosition );
		ProcessJobs();

		// Rebuild painted layer if needed (during painting)
		if ( _dirty )
		{
			RebuildPaintedLayer();
			_dirty = false;
		}
	}

	private void SubscribeToTerrains()
	{
		foreach ( var terrain in Scene.GetAllComponents<Terrain>() )
		{
			if ( SubscribedTerrains.Add( terrain ) )
			{
				terrain.OnTerrainModified += OnTerrainModified;
			}
		}

		// Clean up removed terrains
		SubscribedTerrains.RemoveWhere( t => !t.IsValid() );
	}

	private void UpdateActiveComponents( List<ClutterComponent> components, Vector3 cameraPosition )
	{
		foreach ( var component in components )
		{
			var settings = component.GetCurrentSettings();
			if ( !settings.IsValid )
				continue;

			var layer = GetOrCreateLayer( component, settings );
			layer.UpdateSettings( settings );

			foreach ( var job in layer.UpdateTiles( cameraPosition ) )
				QueueJob( job );
		}
	}

	private void RemoveInactiveComponents( List<ClutterComponent> activeInfiniteComponents )
	{
		// Only remove components that are:
		// 1. Infinite mode AND not in the active list, OR
		// 2. Invalid/destroyed
		var toRemove = ComponentToLayer.Keys
			.Where( c => !c.IsValid() || (c.Infinite && !activeInfiniteComponents.Contains( c )) )
			.ToList();

		foreach ( var component in toRemove )
		{
			ComponentToLayer[component].ClearAllTiles();
			ComponentToLayer.Remove( component );
		}
	}

	private void UpdateInfiniteLayers( Vector3 cameraPosition )
	{
		var activeComponents = Scene.GetAllComponents<ClutterComponent>()
			.Where( c => c.Active && c.Infinite )
			.ToList();

		RemoveInactiveComponents( activeComponents );
		UpdateActiveComponents( activeComponents, cameraPosition );
	}

	/// <summary>
	/// Queues a generation job for processing.
	/// </summary>
	public void QueueJob( ClutterGenerationJob job )
	{
		if ( job?.Clutter?.Scatterer == null || !job.ParentObject.IsValid() )
			return;

		// Prevent duplicate jobs for the same tile
		if ( job.TileData != null )
		{
			if ( !PendingTiles.Add( job.TileData ) )
				return;
		}

		PendingJobs.Add( job );
	}

	/// <summary>
	/// Removes a tile from pending set (called when tile is destroyed).
	/// </summary>
	internal void RemovePendingTile( ClutterTile tile )
	{
		PendingTiles.Remove( tile );

		// Remove any pending jobs for this tile
		PendingJobs.RemoveAll( job => job.TileData == tile );
	}

	/// <summary>
	/// Clears all tiles for a specific component.
	/// </summary>
	public void ClearComponent( ClutterComponent component )
	{
		// Remove any pending jobs for this component (both tile and volume jobs)
		PendingJobs.RemoveAll( job => job.ParentObject == component.GameObject );

		if ( ComponentToLayer.Remove( component, out var layer ) )
		{
			layer.ClearAllTiles();
		}
	}

	/// <summary>
	/// Invalidates the tile at the given world position for a component, causing it to regenerate.
	/// </summary>
	public void InvalidateTileAt( ClutterComponent component, Vector3 worldPosition )
	{
		if ( ComponentToLayer.TryGetValue( component, out var layer ) )
		{
			layer.InvalidateTile( worldPosition );
		}
	}

	/// <summary>
	/// Invalidates all tiles within the given bounds for a component, causing them to regenerate.
	/// </summary>
	public void InvalidateTilesInBounds( ClutterComponent component, BBox bounds )
	{
		if ( ComponentToLayer.TryGetValue( component, out var layer ) )
		{
			layer.InvalidateTilesInBounds( bounds );
		}
	}

	/// <summary>
	/// Invalidates all tiles within the given bounds for ALL infinite clutter components.
	/// Useful for terrain painting where you want to refresh all clutter layers.
	/// </summary>
	public void InvalidateTilesInBounds( BBox bounds )
	{
		foreach ( var layer in ComponentToLayer.Values )
		{
			layer.InvalidateTilesInBounds( bounds );
		}
	}

	private void OnTerrainModified( Terrain.SyncFlags flags, RectInt region )
	{
		// Convert terrain region to world bounds
		var bounds = TerrainRegionToWorldBounds( SubscribedTerrains.First(), region );

		// Invalidate all clutter tiles in this region
		InvalidateTilesInBounds( bounds );
	}

	private static BBox TerrainRegionToWorldBounds( Terrain terrain, RectInt region )
	{
		var terrainTransform = terrain.WorldTransform;
		var storage = terrain.Storage;

		// Convert pixel coordinates to normalized (0-1) coordinates
		var minNorm = new Vector2(
			(float)region.Left / storage.Resolution,
			(float)region.Top / storage.Resolution
		);
		var maxNorm = new Vector2(
			(float)region.Right / storage.Resolution,
			(float)region.Bottom / storage.Resolution
		);

		// Convert to world position relative to terrain
		var terrainSize = storage.TerrainSize;
		var minLocal = new Vector3( minNorm.x * terrainSize, minNorm.y * terrainSize, -1000f );
		var maxLocal = new Vector3( maxNorm.x * terrainSize, maxNorm.y * terrainSize, 1000f );

		// Transform to world space
		var minWorld = terrainTransform.PointToWorld( minLocal );
		var maxWorld = terrainTransform.PointToWorld( maxLocal );

		return new BBox( minWorld, maxWorld );
	}

	private CameraComponent GetActiveCamera()
	{
		return Scene.IsEditor
			? Scene.Camera
			: Scene.Camera; // Figure out a way to grab editor camera
	}

	private ClutterLayer GetOrCreateLayer( ClutterComponent component, ClutterSettings settings )
	{
		if ( ComponentToLayer.TryGetValue( component, out var layer ) )
			return layer;

		layer = new ClutterLayer( settings, component.GameObject, this );
		ComponentToLayer[component] = layer;
		return layer;
	}

	/// <summary>
	/// Public accessor for getting or creating a layer for a component.
	/// Used by volume mode to create layers for batching.
	/// </summary>
	public ClutterLayer GetOrCreateLayerForComponent( ClutterComponent component, ClutterSettings settings )
	{
		return GetOrCreateLayer( component, settings );
	}

	private void ProcessJobs()
	{
		if ( PendingJobs.Count == 0 )
			return;

		// Track which layers had tiles populated
		HashSet<ClutterLayer> layersToRebuild = [];

		PendingJobs.RemoveAll( job =>
			!job.ParentObject.IsValid() ||
			job.TileData?.IsPopulated == true
		);

		// Sort by distance to camera (nearest first)
		PendingJobs.Sort( ( a, b ) =>
		{
			var distA = a.TileData != null
				? a.TileData.Bounds.Center.Distance( LastCameraPosition )
				: float.MaxValue;
			var distB = b.TileData != null
				? b.TileData.Bounds.Center.Distance( LastCameraPosition )
				: float.MaxValue;
			return distA.CompareTo( distB );
		} );

		// Process nearest tiles first
		int processed = 0;
		while ( processed < MAX_JOBS_PER_FRAME && PendingJobs.Count > 0 )
		{
			var job = PendingJobs[0];
			PendingJobs.RemoveAt( 0 );

			// Remove from pending set
			if ( job.TileData != null )
				PendingTiles.Remove( job.TileData );

			// Execute if still valid and not populated
			if ( job.ParentObject.IsValid() && job.TileData?.IsPopulated != true )
			{
				job.Execute();
				processed++;

				// Track layer for batch rebuild
				if ( job.Layer != null )
					layersToRebuild.Add( job.Layer );
			}
		}

		// Rebuild batches for layers that had tiles populated
		foreach ( var layer in layersToRebuild )
		{
			layer.RebuildBatches();
		}

		// If we still have too many jobs, cull the furthest ones
		if ( PendingJobs.Count > MAX_PENDING_JOBS )
		{
			// Keep only the nearest 100 jobs
			var toRemove = PendingJobs.Skip( MAX_PENDING_JOBS ).ToList();
			foreach ( var job in toRemove )
			{
				if ( job.TileData != null )
					PendingTiles.Remove( job.TileData );
			}

			PendingJobs.RemoveRange( MAX_PENDING_JOBS, PendingJobs.Count - MAX_PENDING_JOBS );
		}
	}

	/// <summary>
	/// Paint instance. Rebuilds on next frame update.
	/// Models are batched, Prefabs become GameObjects.
	/// </summary>
	public void Paint( ClutterEntry entry, Vector3 pos, Rotation rot, float scale = 1f )
	{
		if ( entry == null || !entry.HasAsset ) return;
		
		// Prefabs create real GameObjects
		if ( entry.Prefab != null )
		{
			var go = entry.Prefab.Clone( pos, rot );
			go.WorldScale = scale;
			go.SetParent( Scene );
			go.Tags.Add( "clutter" );
		}
		// Models are batched & stored
		else if ( entry.Model != null )
		{
			_storage.AddInstance( entry.Model.ResourcePath, pos, rot, scale );
			_dirty = true;
		}
	}

	/// <summary>
	/// Erase instances. Rebuilds on next frame update.
	/// Erases both model batches and prefab GameObjects.
	/// </summary>
	public void Erase( Vector3 pos, float radius )
	{
		var radiusSquared = radius * radius;
		var erased = false;

		// Erase model instances from storage
		if ( _storage.Erase( pos, radius ) > 0 )
		{
			_dirty = true;
			erased = true;
		}

		// Erase prefab GameObjects tagged as clutter
		var clutterObjects = Scene.GetAllObjects( true )
			.Where( go => go.Tags.Has( "clutter" ) && 
			              go.WorldPosition.DistanceSquared( pos ) <= radiusSquared )
			.ToList();

		foreach ( var go in clutterObjects )
		{
			go.Destroy();
			erased = true;
		}
	}

	/// <summary>
	/// Flush painted changes and rebuild visual batches immediately.
	/// </summary>
	public void Flush()
	{
		RebuildPaintedLayer();
		_dirty = false;
	}

	/// <summary>
	/// Rebuild the painted clutter layer from stored instances.
	/// Called automatically on scene load and after painting.
	/// </summary>
	private void RebuildPaintedLayer()
	{
		if ( _storage.TotalCount == 0 )
		{
			_painted?.ClearAllTiles();
			return;
		}

		// Create or reuse painted layer with minimal settings
		if ( _painted == null )
		{
			var dummyClutter = new ClutterDefinition();
			var settings = new ClutterSettings { Clutter = dummyClutter };
			_painted = new ClutterLayer( settings, Scene, this );
		}
		
		_painted.ClearAllTiles();

		foreach ( var modelPath in _storage.ModelPaths )
		{
			var model = ResourceLibrary.Get<Model>( modelPath );
			if ( model == null ) continue;

			foreach ( var instance in _storage.GetInstances( modelPath ) )
			{
				_painted.AddModelInstance( Vector2Int.Zero, new()
				{
					Transform = new( instance.Position, instance.Rotation, instance.Scale ),
					Entry = new() { Model = model }
				} );
			}
		}

		_painted.RebuildBatches();
	}
}
