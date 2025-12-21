using System;
using System.Collections.Generic;

namespace Sandbox.Clutter;

/// <summary>
/// Simple job for clutter generation work.
/// Executes synchronously when processed.
/// </summary>
public class ClutterGenerationJob
{
	public ClutterDefinition Clutter { get; init; }
	public GameObject ParentObject { get; init; }

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
	public static ClutterGenerationJob Volume( BBox bounds, float cellSize, int randomSeed, ClutterDefinition clutter, GameObject parentObject )
	{
		return new ClutterGenerationJob
		{
			Bounds = bounds,
			CellSize = cellSize,
			RandomSeed = randomSeed,
			Clutter = clutter,
			ParentObject = parentObject
		};
	}

	/// <summary>
	/// Creates a job for tile generation.
	/// </summary>
	public static ClutterGenerationJob Tile( BBox bounds, ClutterTile tile, int randomSeed, ClutterDefinition clutter, GameObject parentObject )
	{
		return new ClutterGenerationJob
		{
			Bounds = bounds,
			TileData = tile,
			RandomSeed = randomSeed,
			Clutter = clutter,
			ParentObject = parentObject
		};
	}

	public void Execute()
	{
		if ( !ParentObject.IsValid() || Clutter?.Scatterer == null )
			return;

		var instances = TileData != null 
			? ScatterTile() 
			: ScatterVolume();

		SpawnInstances( instances );

		if ( TileData != null )
			TileData.IsPopulated = true;
	}

	private List<ClutterInstance> ScatterTile()
	{
		var seed = Scatterer.GenerateSeed( TileData.SeedOffset, TileData.Coordinates.x, TileData.Coordinates.y );
		return Clutter.Scatterer.Scatter( Bounds, Clutter, seed, ParentObject.Scene );
	}

	private List<ClutterInstance> ScatterVolume()
	{
		var instances = new List<ClutterInstance>();
		var cellsX = (int)MathF.Ceiling( Bounds.Size.x / CellSize );
		var cellsY = (int)MathF.Ceiling( Bounds.Size.y / CellSize );
		var cellsZ = (int)MathF.Ceiling( Bounds.Size.z / CellSize );

		for ( int x = 0; x < cellsX; x++ )
		for ( int y = 0; y < cellsY; y++ )
		for ( int z = 0; z < cellsZ; z++ )
		{
			var cellBounds = GetCellBounds( x, y, z );
			var cellSeed = HashCode.Combine( RandomSeed, x, y, z );
			instances.AddRange( Clutter.Scatterer.Scatter( cellBounds, Clutter, cellSeed, ParentObject.Scene ) );
		}

		return instances;
	}

	private void SpawnInstances( List<ClutterInstance> instances )
	{
		using ( ParentObject.Scene.Push() )
		{
			foreach ( var instance in instances )
			{
				if ( instance.Entry.Prefab == null ) 
					continue;

				var obj = instance.Entry.Prefab.Clone( instance.Transform, ParentObject.Scene );
				obj.Flags |= GameObjectFlags.NotSaved;
				obj.Flags |= GameObjectFlags.Hidden;
				obj.Tags.Add( "clutter" );
				obj.SetParent( ParentObject );
				TileData?.AddObject( obj );
			}
		}
	}

	private BBox GetCellBounds( int x, int y, int z )
	{
		var min = Bounds.Mins + new Vector3( x, y, z ) * CellSize;
		var max = Vector3.Min( Bounds.Maxs, min + CellSize );
		return new BBox( min, max );
	}
}
