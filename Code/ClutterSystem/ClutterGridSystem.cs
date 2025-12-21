using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Clutter;

/// <summary>
/// Game object system that manages clutter generation.
/// Handles infinite streaming layers and executes generation jobs.
/// </summary>
public sealed class ClutterGridSystem : GameObjectSystem
{
	private Dictionary<ClutterComponent, ClutterLayer> ComponentToLayer { get; set; } = [];
	private List<ClutterGenerationJob> PendingJobs { get; set; } = [];
	private HashSet<ClutterTile> PendingTiles { get; set; } = [];
	private HashSet<Terrain> SubscribedTerrains { get; set; } = [];
	private Vector3 LastCameraPosition { get; set; }
	private const int MAX_JOBS_PER_FRAME = 8;

	public ClutterGridSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ClutterGridSystem.Update" );
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

	private void OnUpdate()
	{
		var camera = GetActiveCamera();
		if ( camera == null )
			return;

		LastCameraPosition = camera.WorldPosition;

		SubscribeToTerrains();
		UpdateInfiniteLayers( LastCameraPosition );
		ProcessJobs();
	}

	private void SubscribeToTerrains()
	{
		var terrains = Scene.GetAllComponents<Terrain>();

		foreach ( var terrain in terrains )
		{
			if ( SubscribedTerrains.Add( terrain ) )
			{
				terrain.OnTerrainModified += OnTerrainModified;
			}
		}

		// Clean up removed terrains
		var toRemove = SubscribedTerrains.Where( t => !t.IsValid() ).ToList();
		foreach ( var terrain in toRemove )
		{
			SubscribedTerrains.Remove( terrain );
		}
	}

	private void OnTerrainModified( Terrain.SyncFlags flags, RectInt region )
	{
		// Only care about control map changes (material painting)
		//if ( !flags.HasFlag( Terrain.SyncFlags.Control ) )
		//	return;

		// Convert terrain region to world bounds
		var bounds = TerrainRegionToWorldBounds( SubscribedTerrains.First(), region );

		// Invalidate all clutter tiles in this region
		InvalidateTilesInBounds( bounds );
	}

	private BBox TerrainRegionToWorldBounds( Terrain terrain, RectInt region )
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

	private void UpdateInfiniteLayers( Vector3 cameraPosition )
	{
		var activeComponents = Scene.GetAllComponents<ClutterComponent>()
			.Where( c => c.Active && c.Infinite )
			.ToList();

		RemoveInactiveComponents( activeComponents );
		UpdateActiveComponents( activeComponents, cameraPosition );
	}

	private void RemoveInactiveComponents( List<ClutterComponent> activeComponents )
	{
		var toRemove = ComponentToLayer.Keys.Except( activeComponents ).ToList();

		foreach ( var component in toRemove )
		{
			ComponentToLayer[component].ClearAllTiles();
			ComponentToLayer.Remove( component );
		}
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

	private ClutterLayer GetOrCreateLayer( ClutterComponent component, ClutterSettings settings )
	{
		if ( ComponentToLayer.TryGetValue( component, out var layer ) )
			return layer;

		layer = new ClutterLayer( settings, component.GameObject, this );
		ComponentToLayer[component] = layer;
		return layer;
	}

	private void ProcessJobs()
	{
		if ( PendingJobs.Count == 0 )
			return;

		// Remove invalid jobs
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
			}
		}

		// If we still have too many jobs, cull the furthest ones
		const int MAX_PENDING_JOBS = 100;
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
}
