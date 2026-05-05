using System;

namespace Voxels;

public readonly ref struct ReadOnlyVoxelSpan<T>
{
	public static implicit operator ReadOnlyVoxelSpan<T>( VoxelSpan<T> span )
	{
		return new ReadOnlyVoxelSpan<T>( span.Source, span.Size, span.Stride );
	}

	public static VoxelSpan<T> Empty => default;

	internal ReadOnlySpan<T> Source { get; }

	public Vector3Int Size { get; }
	public Vector2Int Stride { get; }

	public bool IsEmpty => Source.IsEmpty;

	public ReadOnlyVoxelSpan( ReadOnlySpan<T> source, Vector3Int size )
		: this( source, size, new Vector2Int( size.x, size.x * size.y ) )
	{

	}

	public ReadOnlyVoxelSpan( ReadOnlySpan<T> source, Vector3Int size, Vector2Int stride )
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( size.x );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( size.y );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( size.z );

		ArgumentOutOfRangeException.ThrowIfLessThan( stride.x, size.x );
		ArgumentOutOfRangeException.ThrowIfLessThan( stride.y, size.x * size.y );

		Size = size;
		Stride = stride;

		Source = source[..(GetSourceIndex( size.x - 1, size.y, size.z ) + 1)];
	}

	private int GetSourceIndex( int x, int y, int z ) => x + y * Stride.x + z * Stride.y;
	private int GetSourceIndex( Vector3Int index ) => GetSourceIndex( index.x, index.y, index.z );

	public T this[ int x, int y, int z ] => Source[GetSourceIndex( x, y, z )];

	public T this[ Vector3Int index ] => Source[GetSourceIndex( index )];

	public ReadOnlyVoxelSpan<T> Slice( Vector3Int offset, Vector3Int size )
	{
		if ( offset == default && size == Size )
		{
			return this;
		}

		var start = GetSourceIndex( offset );
		var end = GetSourceIndex( offset.x + size.x - 1, offset.y + size.y, offset.z + size.z ) + 1;

		return new ReadOnlyVoxelSpan<T>( Source.Slice( start, end - start ), size, Stride );
	}

	public void CopyTo( VoxelSpan<T> span )
	{
		span = span.Slice( default, Size );

		if ( Stride == span.Stride && Stride.y == Size.x * Size.y )
		{
			Source.CopyTo( span.Source );
			return;
		}

		if ( Stride.x == span.Stride.x && Stride.x == Size.x )
		{
			for ( var z = 0; z < Size.z; z++ )
			{
				var thisStart = GetSourceIndex( 0, 0, z );
				var otherStart = span.GetSourceIndex( 0, 0, z );
				var sliceLength = Stride.x * Size.y;

				Source.Slice( thisStart, sliceLength ).CopyTo( span.Source.Slice( otherStart, sliceLength ) );
			}
			return;
		}

		for ( var z = 0; z < Size.z; z++ )
		{
			for ( var y = 0; y < Size.y; y++ )
			{
				var thisStart = GetSourceIndex( 0, y, z );
				var otherStart = span.GetSourceIndex( 0, y, z );

				Source.Slice( thisStart, Size.x ).CopyTo( span.Source.Slice( otherStart, Size.x ) );
			}
		}
	}
}
