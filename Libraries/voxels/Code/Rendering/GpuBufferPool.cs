using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Voxels.Rendering;

internal sealed class GpuBufferPool<T>
	where T : unmanaged
{
	private readonly GpuBuffer.UsageFlags _flags;
	private readonly int _capacity;

	private readonly SortedList<int, List<GpuBuffer<T>>> _buffers = new();
	private readonly HashSet<GpuBuffer<T>> _pooled = new();

	public GpuBufferPool( GpuBuffer.UsageFlags flags = GpuBuffer.UsageFlags.Structured, int capacity = 128 )
	{
		_flags = flags;
		_capacity = capacity;
	}

	public GpuBuffer<T> Rent( int size )
	{
		var largestPooledSize = _buffers.LastOrDefault().Key;

		size = size.NextPowerOf2;

		while ( size < largestPooledSize && !_buffers.ContainsKey( size ) )
		{
			size <<= 1;
		}

		if ( !_buffers.TryGetValue( size, out var buffers ) )
		{
			return new GpuBuffer<T>( size, _flags );
		}

		var buffer = buffers[^1];
		buffers.RemoveAt( buffers.Count - 1 );

		if ( !_pooled.Remove( buffer ) )
		{
			throw new Exception( "Buffer was removed from the pool more than once!" );
		}

		if ( buffers.Count == 0 )
		{
			_buffers.Remove( size );
		}

		return buffer;
	}

	public void Return( GpuBuffer<T> buffer )
	{
		if ( _pooled.Count >= _capacity )
		{
			var smallestPooledSize = _buffers.FirstOrDefault().Key;

			if ( smallestPooledSize >= buffer.ElementCount )
			{
				buffer.Dispose();
				return;
			}

			// Free up space for this larger buffer

			Rent( smallestPooledSize ).Dispose();
		}

		if ( !_pooled.Add( buffer ) )
		{
			throw new Exception( "Buffer was already pooled!" );
		}

		if ( !_buffers.TryGetValue( buffer.ElementCount, out var buffers ) )
		{
			_buffers[buffer.ElementCount] = buffers = new List<GpuBuffer<T>>();
		}

		buffers.Add( buffer );
	}
}

