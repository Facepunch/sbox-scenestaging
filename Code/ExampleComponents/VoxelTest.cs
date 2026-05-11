using Voxels.Rendering;

namespace Sandbox;

public sealed class VoxelTest : Component, Component.ExecuteInEditor
{
	private readonly Dictionary<Vector3Int, SceneCubicVoxelsObject> _chunks = new();

	[Property]
	public float VoxelSize { get; set; } = 32f;

	[Property]
	public Vector3Int ChunkCount { get; set; } = new Vector3Int( 16, 16, 4 );

	[Property]
	public Vector3Int ChunkSize { get; set; } = 32;

	[Property]
	public Vector3Int Offset { get; set; }

	[Property]
	public int Seed { get; set; } = 12379162;

	[Button]
	public void RandomizeSeed()
	{
		Seed = Random.Shared.Next();
		UpdateChunks();
	}

	[Button]
	public void UpdateChunks()
	{
		_ = UpdateChunksAsync();
	}

	public void Subtract( Vector3 position, float radius )
	{
		var localPosition = position / VoxelSize;
		var localRadius = radius / VoxelSize;

		var localMin = new Vector3Int(
			(localPosition.x - localRadius).FloorToInt(),
			(localPosition.y - localRadius).FloorToInt(),
			(localPosition.z - localRadius).FloorToInt() );

		var localMax = new Vector3Int(
			(localPosition.x + localRadius).CeilToInt(),
			(localPosition.y + localRadius).CeilToInt(),
			(localPosition.z + localRadius).CeilToInt() );

		var chunkMin = GetChunkIndex( localMin - 1 );
		var chunkMax = GetChunkIndex( localMax ) + 1;

		for ( var cz = chunkMin.z; cz < chunkMax.z; cz++ )
		{
			for ( var cy = chunkMin.y; cy < chunkMax.y; cy++ )
			{
				for ( var cx = chunkMin.x; cx < chunkMax.x; cx++ )
				{
					var chunkIndex = new Vector3Int( cx, cy, cz );

					if ( !_chunks.TryGetValue( chunkIndex, out var chunk ) ) continue;

					chunk.Subtract( localPosition - chunkIndex * ChunkSize, localRadius );
					Scene.GetSystem<VoxelRenderingSystem>().QueueChunkUpdate( chunk );
				}
			}
		}
	}

	private Vector3Int GetChunkIndex( Vector3Int localIndex )
	{
		return new Vector3Int(
			GetChunkIndexComponent( localIndex.x, ChunkSize.x ),
			GetChunkIndexComponent( localIndex.y, ChunkSize.y ),
			GetChunkIndexComponent( localIndex.z, ChunkSize.z ) );
	}

	private static int GetChunkIndexComponent( int local, int chunkSize )
	{
		if ( local >= 0 ) return local / chunkSize;
		return (local - chunkSize + 1) / chunkSize;
	}

	private async Task UpdateChunksAsync()
	{
		var min = -ChunkCount / 2;
		var max = min + ChunkCount;

		var toRemove = _chunks.Keys
			.Where( pos => pos.x < min.x || pos.y < min.y || pos.z < min.z || pos.x >= max.x || pos.y >= max.y || pos.z >= max.z )
			.ToArray();

		foreach ( var pos in toRemove )
		{
			_chunks.Remove( pos, out var chunk );
			chunk?.Delete();
		}

		var toGenerate = Enumerable.Range( min.x, ChunkCount.x )
			.SelectMany( x => Enumerable.Range( min.y, ChunkCount.y )
				.SelectMany( y => Enumerable.Range( min.z, ChunkCount.z )
					.Select( z => new Vector3Int( x, y, z ) ) ) )
			.Shuffle()
			.ToArray();

		foreach ( var pos in toGenerate )
		{
			if ( !_chunks.TryGetValue( pos, out var chunk ) )
			{
				chunk = _chunks[pos] = new SceneCubicVoxelsObject( Scene.SceneWorld );
			}

			await Task.MainThread();

			try
			{
				chunk.VoxelSize = VoxelSize;
				chunk.Generate( ChunkSize, ChunkSize * pos, Seed );
				Scene.GetSystem<VoxelRenderingSystem>().QueueChunkUpdate( chunk );
			}
			catch ( Exception ex )
			{
				Log.Error( ex );
			}

			await Task.Yield();
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
			chunk.Delete();
		}

		_chunks.Clear();
	}
}
