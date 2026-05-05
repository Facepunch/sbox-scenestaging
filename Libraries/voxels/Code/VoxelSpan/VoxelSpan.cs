using System;

namespace Voxels;

public readonly ref struct VoxelSpan<T>
{
	public static VoxelSpan<T> Empty => default;

	internal Span<T> Source { get; }

	public Vector3Int Size { get; }
	public Vector2Int Stride { get; }

	public bool IsEmpty => Source.IsEmpty;

	public VoxelSpan( Span<T> source, Vector3Int size )
		: this( source, size, new Vector2Int( size.x, size.x * size.y ) )
	{

	}

	public VoxelSpan( Span<T> source, Vector3Int size, Vector2Int stride )
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( size.x );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( size.y );
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero( size.z );

		ArgumentOutOfRangeException.ThrowIfLessThan( stride.x, size.x );
		ArgumentOutOfRangeException.ThrowIfLessThan( stride.y, size.x * size.y );

		Size = size;
		Stride = stride;

		Source = source[..GetSourceIndex( size.x, size.y - 1, size.z - 1 )];
	}

	internal int GetSourceIndex( int x, int y, int z ) => x + y * Stride.x + z * Stride.y;
	internal int GetSourceIndex( Vector3Int index ) => GetSourceIndex( index.x, index.y, index.z );

	public T this[ int x, int y, int z ]
	{
		get => Source[GetSourceIndex( x, y, z )];
		set => Source[GetSourceIndex( x, y, z )] = value;
	}

	public T this[ Vector3Int index ]
	{
		get => Source[GetSourceIndex( index )];
		set => Source[GetSourceIndex( index )] = value;
	}

	public VoxelSpan<T> Slice( Vector3Int offset, Vector3Int size )
	{
		if ( offset == default && size == Size )
		{
			return this;
		}

		var start = GetSourceIndex( offset );
		var end = GetSourceIndex( offset.x + size.x, offset.y + size.y - 1, offset.z + size.z - 1 );

		return new VoxelSpan<T>( Source.Slice( start, end - start ), size, Stride );
	}

	public void CopyTo( VoxelSpan<T> other ) => ((ReadOnlyVoxelSpan<T>)this).CopyTo( other );

	public void Fill( T value )
	{
		if ( Stride.y == Size.x * Size.y )
		{
			Source.Fill( value );
			return;
		}

		if ( Stride.x == Size.x )
		{
			for ( var z = 0; z < Size.z; z++ )
			{
				var start = GetSourceIndex( 0, 0, z );
				Source.Slice( start, Stride.x * Size.y ).Fill( value );
			}

			return;
		}

		for ( var z = 0; z < Size.z; z++ )
		{
			for ( var y = 0; y < Size.y; y++ )
			{
				var start = GetSourceIndex( 0, y, z );

				Source.Slice( start, Size.x ).Fill( value );
			}
		}
	}

	public void Clear()
	{
		Fill( default! );
	}
}
