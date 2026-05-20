using Sandbox;
using System;
using System.Collections.Generic;
using Voxels.Rendering;

namespace Voxels;

public sealed class VoxelVolume : Component, Component.ExecuteInEditor
{
	private readonly Dictionary<ChunkIndex, VoxelChunk> _chunks = new();

	[Property]
	public float VoxelSize { get; set; } = 32f;

	[Property]
	public Vector3Int Offset { get; set; }

	[Property]
	public int Seed { get; set; } = 12379162;

	[Property]
	public int MaxLevel { get; set; } = 4;

	[Property]
	public int ChunkLoadRadius { get; set; } = 2;

	[Property]
	public bool PauseChunkUpdates { get; set; }

	[Button]
	public void RandomizeSeed()
	{
		Seed = Random.Shared.Next();
		UpdateChunks();
	}

	private Vector3Int GetLoadOrigin()
	{
		var cameraPos = Scene.Camera.IsValid() ? Scene.Camera.WorldPosition : default;
		return (Vector3Int)(cameraPos / VoxelSize);
	}

	private Vector3Int _lastLoadOrigin;

	protected override void OnUpdate()
	{
		if ( PauseChunkUpdates ) return;

		var loadOrigin = GetLoadOrigin();

		if ( _lastLoadOrigin != loadOrigin )
		{
			_lastLoadOrigin = loadOrigin;
			UpdateChunks();
		}

		foreach ( var (index, chunk) in _chunks )
		{
			chunk.LodDistance = index.Level > 0 ? ChunkLoadRadius * index.VoxelScale * VoxelChunk.Size * VoxelSize * 0.25f : 0f;
			chunk.LodMask = GetLodMask( index );
		}
	}

	private ChunkIndex GetChunkIndex( Vector3Int localIndex, int level )
	{
		return new ChunkIndex( new Vector3Int(
			GetChunkIndexComponent( localIndex.x, VoxelChunk.Size << level ),
			GetChunkIndexComponent( localIndex.y, VoxelChunk.Size << level ),
			GetChunkIndexComponent( localIndex.z, VoxelChunk.Size << level ) ), level );
	}

	private static int GetChunkIndexComponent( int local, int chunkSize )
	{
		if ( local >= 0 ) return local / chunkSize;
		return (local - chunkSize + 1) / chunkSize;
	}

	private readonly HashSet<ChunkIndex> _chunksToLoad = new();
	private readonly HashSet<ChunkIndex> _chunksToKeep = new();
	private readonly HashSet<ChunkIndex> _chunksToRemove = new();

	[Button]
	public void UpdateChunks()
	{
		FindChunksToLoad();
		UnloadUnwantedChunks();

		foreach ( var index in _chunksToLoad )
		{
			if ( !_chunks.TryGetValue( index, out var chunk ) )
			{
				chunk = _chunks[index] = new VoxelChunk( this, index, index.VoxelScale * VoxelSize );
				chunk.Generate( (Vector3Int)(index.Min * VoxelSize), Seed );
			}

			if ( index.Level == 0 )
			{
				// TODO: enable physics
			}
		}
	}

	private byte GetLodMask( ChunkIndex index )
	{
		var firstSubChunk = index.FirstSubChunk;
		byte mask = 0;

		for ( var i = 0; i < 8; i++ )
		{
			var offset = new Vector3Int( i & 1, (i >> 1) & 1, (i >> 2) & 1 );
			var subChunk = new ChunkIndex( firstSubChunk.Position + offset, firstSubChunk.Level );

			if ( !_chunks.TryGetValue( subChunk, out var chunk ) ) continue;
			if ( !chunk.IsReady ) continue;

			mask |= (byte)(1 << i);
		}

		return mask;
	}

	private void FindChunksToLoad()
	{
		var loadOrigin = GetLoadOrigin();

		_chunksToLoad.Clear();
		_chunksToKeep.Clear();

		for ( var i = 0; i <= MaxLevel; i++ )
		{
			FindChunksToLoad( loadOrigin, ChunkLoadRadius, i, _chunksToLoad );
			FindChunksToLoad( loadOrigin, ChunkLoadRadius + 1, i, _chunksToKeep );
		}
	}

	private void FindChunksToLoad( Vector3Int localPos, int loadRadius, int level, HashSet<ChunkIndex> result )
	{
		var origin = GetChunkIndex( localPos, level );

		var minIndex = origin.Position - loadRadius;
		var maxIndex = origin.Position + loadRadius;

		var thresholdSq = (loadRadius + 1) * (loadRadius + 1);

		for ( var z = minIndex.z; z <= maxIndex.z; z++ )
		{
			for ( var y = minIndex.y; y <= maxIndex.y; y++ )
			{
				for ( var x = minIndex.x; x <= maxIndex.x; x++ )
				{
					var index = new Vector3Int( x, y, z );

					if ( (index - origin.Position).LengthSquared < thresholdSq )
					{
						result.Add( new ChunkIndex( index, level ) );
					}
				}
			}
		}
	}

	private void UnloadUnwantedChunks()
	{
		_chunksToRemove.Clear();

		foreach ( var index in _chunks.Keys )
		{
			if ( !_chunksToKeep.Contains( index ) )
			{
				_chunksToRemove.Add( index );
			}
		}

		foreach ( var index in _chunksToRemove )
		{
			_chunks.Remove( index, out var chunk );
			chunk?.Dispose();
		}
	}

	protected override void OnEnabled()
	{
		UpdateChunks();
	}

	protected override void OnDisabled()
	{
		Clear();
	}

	protected override void OnDestroy()
	{
		Clear();
	}

	private void Clear()
	{
		foreach ( var chunk in _chunks.Values )
		{
			chunk.Dispose();
		}

		_chunks.Clear();
	}

	public bool AreChunksLoaded( BBox worldBounds )
	{
		var minIndex = GetChunkIndex( (Vector3Int)(worldBounds.Mins / VoxelSize), 0 ).Position;
		var maxIndex = GetChunkIndex( (Vector3Int)(worldBounds.Maxs / VoxelSize), 0 ).Position;

		for ( var z = minIndex.z; z <= maxIndex.z; z++ )
		{
			for ( var y = minIndex.y; y <= maxIndex.y; y++ )
			{
				for ( var x = minIndex.x; x <= maxIndex.x; x++ )
				{
					var index = new ChunkIndex( new Vector3Int( x, y, z ), 0 );

					if ( !_chunks.TryGetValue( index, out var chunk ) ) return false;

					if ( !chunk.IsPhysicsReady )
					{
						return false;
					}
				}
			}
		}

		return true;
	}
}
