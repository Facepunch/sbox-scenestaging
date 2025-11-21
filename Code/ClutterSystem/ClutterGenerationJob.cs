using System;

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
		var initialChildCount = ParentObject?.Children.Count ?? 0;

		if ( TileData != null )
		{
			// Tile mode - generate entire tile
			await GameTask.WorkerThread();
			Isotope.Scatterer.Scatter( Bounds, Isotope, TileData, ParentObject );
			await GameTask.MainThread();
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
						var cellSeed = GetCellSeed( RandomSeed, x, y, z );
						var random = new Random( cellSeed );

						Isotope.Scatterer.ScatterInVolume( cellBounds, Isotope, ParentObject, random );
					}
				}
			}

			await GameTask.MainThread();
		}

		var spawnedCount = (ParentObject?.Children.Count ?? 0) - initialChildCount;
		OnComplete?.Invoke( spawnedCount );
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

	private static int GetCellSeed( int randomSeed, int x, int y, int z )
	{
		int seed = randomSeed;
		seed = (seed * 397) ^ x;
		seed = (seed * 397) ^ y;
		seed = (seed * 397) ^ z;
		return seed;
	}
}
