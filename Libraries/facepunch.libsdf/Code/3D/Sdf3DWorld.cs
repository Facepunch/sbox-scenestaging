using System;
using System.Collections.Generic;

namespace Sandbox.Sdf;

/// <summary>
/// Main entity for creating a 3D surface that can be added to and subtracted from.
/// Multiple volumes can be added to this entity with different materials.
/// </summary>
[Title( "SDF 3D World" )]
public partial class Sdf3DWorld : SdfWorld<Sdf3DWorld, Sdf3DChunk, Sdf3DVolume, (int X, int Y, int Z), Sdf3DArray, ISdf3D>
{
	public override int Dimensions => 3;

	[Property]
	public bool IsFinite { get; set; }

	[Property, ShowIf( nameof(IsFinite), true )]
	public Vector3 Size { get; set; } = new Vector3( 1024, 1024, 1024 );

	private ((int X, int Y, int Z) Min, (int X, int Y, int Z) Max) GetChunkRange( BBox bounds, WorldQuality quality )
	{
		var unitSize = quality.UnitSize;

		var min = (bounds.Mins - quality.MaxDistance - unitSize) / quality.ChunkSize;
		var max = (bounds.Maxs + quality.MaxDistance + unitSize) / quality.ChunkSize;

		var minX = (int) MathF.Floor( min.x );
		var minY = (int) MathF.Floor( min.y );
		var minZ = (int) MathF.Floor( min.z );

		var maxX = (int) MathF.Ceiling( max.x );
		var maxY = (int) MathF.Ceiling( max.y );
		var maxZ = (int) MathF.Ceiling( max.z );

		if ( IsFinite )
		{
			var chunksX = (int)MathF.Ceiling( Size.x / quality.ChunkSize );
			var chunksY = (int)MathF.Ceiling( Size.y / quality.ChunkSize );
			var chunksZ = (int)MathF.Ceiling( Size.z / quality.ChunkSize );

			minX = Math.Max( 0, minX );
			minY = Math.Max( 0, minY );
			minZ = Math.Max( 0, minZ );

			maxX = Math.Min( chunksX, maxX );
			maxY = Math.Min( chunksY, maxY );
			maxZ = Math.Min( chunksZ, maxZ );
		}

		return ((minX, minY, minZ), (maxX, maxY, maxZ));
	}

	private IEnumerable<(int X, int Y, int Z)> GetChunks( BBox bounds, WorldQuality quality )
	{
		var ((minX, minY, minZ), (maxX, maxY, maxZ)) = GetChunkRange( bounds, quality );

		for ( var z = minZ; z < maxZ; ++z )
		for ( var y = minY; y < maxY; ++y )
		for ( var x = minX; x < maxX; ++x )
		{
			yield return (x, y, z);
		}
	}

	private BBox? DefaultBounds => IsFinite ? new BBox( 0f, Size ) : null;

	/// <inheritdoc />
	protected override IEnumerable<(int X, int Y, int Z)> GetAffectedChunks<T>( T sdf, WorldQuality quality )
	{
		if ( (sdf.Bounds ?? DefaultBounds) is not { } bounds )
		{
			throw new Exception( "Can only make modifications with an SDF with Bounds != null" );
		}

		return GetChunks( bounds, quality );
	}

	protected override bool AffectsChunk<T>( T sdf, WorldQuality quality, (int X, int Y, int Z) chunkKey )
	{
		if ( (sdf.Bounds ?? DefaultBounds) is not { } bounds )
		{
			throw new Exception( "Can only make modifications with an SDF with Bounds != null" );
		}

		var ((minX, minY, minZ), (maxX, maxY, maxZ)) = GetChunkRange( bounds, quality );
		return chunkKey.X >= minX && chunkKey.X < maxX
			&& chunkKey.Y >= minY && chunkKey.Y < maxY
			&& chunkKey.Z >= minZ && chunkKey.Z < maxZ;
	}
}
