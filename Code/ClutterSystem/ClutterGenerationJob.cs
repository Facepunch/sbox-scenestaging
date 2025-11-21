using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Simple job for clutter generation work.
/// Executes synchronously when processed.
/// </summary>
public class ClutterGenerationJob
{
	public ClutterIsotope Isotope { get; init; }
	public GameObject ParentObject { get; init; }
	public Action<int> OnComplete { get; init; }
	
	// Volume-specific
	public BBox Bounds { get; init; }
	public float CellSize { get; init; }
	public int RandomSeed { get; init; }
	
	// Tile-specific  
	public ClutterTile TileData { get; init; }

	private ClutterGenerationJob() { }

	/// <summary>
	/// Creates a job for volume generation.
	/// </summary>
	public static ClutterGenerationJob Volume( BBox bounds, float cellSize, int randomSeed, ClutterIsotope isotope, GameObject parentObject, Action<int> onComplete = null )
	{
		return new ClutterGenerationJob
		{
			Bounds = bounds,
			CellSize = cellSize,
			RandomSeed = randomSeed,
			Isotope = isotope,
			ParentObject = parentObject,
			OnComplete = onComplete
		};
	}

	/// <summary>
	/// Creates a job for tile generation.
	/// </summary>
	public static ClutterGenerationJob Tile( BBox bounds, ClutterTile tile, int randomSeed, ClutterIsotope isotope, GameObject parentObject )
	{
		return new ClutterGenerationJob
		{
			Bounds = bounds,
			TileData = tile,
			RandomSeed = randomSeed,
			Isotope = isotope,
			ParentObject = parentObject
		};
	}

	/// <summary>
	/// Executes the job immediately.
	/// </summary>
	internal async Task ExecuteAsync()
	{
		List<ClutterInstance> allInstances = new();

		if ( TileData != null )
		{
			// Tile mode - generate entire tile
			await GameTask.WorkerThread();
			
			int seed = Scatterer.GenerateSeed( TileData.SeedOffset, TileData.Coordinates.x, TileData.Coordinates.y );
			allInstances = Isotope.Scatterer.Scatter( Bounds, Isotope, seed );
			
			await GameTask.MainThread();
			SpawnInstances( allInstances, TileData );
		}
		else
		{
			// Volume mode - generate all cells
			var cellsX = (int)MathF.Max( 1, MathF.Ceiling( Bounds.Size.x / CellSize ) );
			var cellsY = (int)MathF.Max( 1, MathF.Ceiling( Bounds.Size.y / CellSize ) );
			var cellsZ = (int)MathF.Max( 1, MathF.Ceiling( Bounds.Size.z / CellSize ) );

			await GameTask.WorkerThread();

			for ( int x = 0; x < cellsX; x++ )
			{
				for ( int y = 0; y < cellsY; y++ )
				{
					for ( int z = 0; z < cellsZ; z++ )
					{
						var cellBounds = GetCellBounds( Bounds, CellSize, x, y, z );
						
						// Combine x, y, z into single seed using same pattern as 2D
						int cellSeed = RandomSeed;
						cellSeed = (cellSeed * 397) ^ x;
						cellSeed = (cellSeed * 397) ^ y;
						cellSeed = (cellSeed * 397) ^ z;

						var cellInstances = Isotope.Scatterer.Scatter( cellBounds, Isotope, cellSeed );
						allInstances.AddRange( cellInstances );
					}
				}
			}

			await GameTask.MainThread();
			SpawnInstances( allInstances, null );
		}

		OnComplete?.Invoke( allInstances.Count );
	}

	/// <summary>
	/// Spawns clutter instances as GameObjects.
	/// Routes Models and Prefabs to their appropriate spawn paths.
	/// </summary>
	private void SpawnInstances( List<ClutterInstance> instances, ClutterTile tile )
	{
		foreach ( var instance in instances )
		{
			if ( instance.Entry.Prefab != null )
			{
				GameObject spawnedObject = instance.Entry.Prefab.Clone( instance.Transform );
				spawnedObject.Flags |= GameObjectFlags.NotSaved;
				spawnedObject.Tags.Add( "clutter" );
				spawnedObject.SetParent( ParentObject );
				tile?.AddObject( spawnedObject );
			}
			else if ( instance.Entry.Model != null )
			{
				
			}

				
			
		}
	}

	private static BBox GetCellBounds( BBox volumeBounds, float cellSize, int x, int y, int z )
	{
		var cellMin = new Vector3(
			volumeBounds.Mins.x + (x * cellSize),
			volumeBounds.Mins.y + (y * cellSize),
			volumeBounds.Mins.z + (z * cellSize)
		);

		var cellMax = new Vector3(
			MathF.Min( volumeBounds.Maxs.x, cellMin.x + cellSize ),
			MathF.Min( volumeBounds.Maxs.y, cellMin.y + cellSize ),
			MathF.Min( volumeBounds.Maxs.z, cellMin.z + cellSize )
		);

		return new BBox( cellMin, cellMax );
	}
}
