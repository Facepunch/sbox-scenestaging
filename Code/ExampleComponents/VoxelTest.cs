using Voxels.Rendering;

namespace Sandbox;

public sealed class VoxelTest : Component, Component.ExecuteInEditor
{
	public const int ChunkSize = 64;

	private readonly record struct ChunkIndex( Vector3Int Index, int Level )
	{
		public Vector3Int Min => Index * ChunkSize * VoxelScale;
		public Vector3Int Max => Index * ChunkSize * VoxelScale;

		public int VoxelScale => 1 << Level;

		public bool Contains( Vector3Int pos )
		{
			var thisMin = Min;
			var thisMax = Max;

			return pos.x >= thisMin.x && pos.x < thisMax.x
				&& pos.y >= thisMin.y && pos.y < thisMax.y
				&& pos.z >= thisMin.z && pos.z < thisMax.z;
		}

		public bool Contains( Vector3Int min, Vector3Int max )
		{
			var thisMin = Min;
			var thisMax = Max;

			return thisMin.x <= max.x && thisMax.x > min.x
				&& thisMin.y <= max.y && thisMax.y > min.y
				&& thisMin.z <= max.z && thisMax.z > min.z;
		}

		public ChunkIndex FirstSubChunk => new ChunkIndex( Index * 2, Level - 1 );
	}

	private readonly Dictionary<ChunkIndex, SceneVoxelsObject> _chunks = new();

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
	}

	public void Subtract( Capsule capsule )
	{
		var localCapsule = new Capsule(
			capsule.CenterA / VoxelSize,
			capsule.CenterB / VoxelSize,
			capsule.Radius / VoxelSize );

		var localBounds = localCapsule.Bounds;

		var localMin = new Vector3Int(
			localBounds.Mins.x.FloorToInt(),
			localBounds.Mins.y.FloorToInt(),
			localBounds.Mins.z.FloorToInt() );

		var localMax = new Vector3Int(
			localBounds.Maxs.x.CeilToInt(),
			localBounds.Maxs.y.CeilToInt(),
			localBounds.Maxs.z.CeilToInt() );

		var chunkMin = GetChunkIndex( localMin - 1, 0 );
		var chunkMax = GetChunkIndex( localMax + 1, 0 );

		for ( var cz = chunkMin.Index.z; cz <= chunkMax.Index.z; cz++ )
		{
			for ( var cy = chunkMin.Index.y; cy <= chunkMax.Index.y; cy++ )
			{
				for ( var cx = chunkMin.Index.x; cx <= chunkMax.Index.x; cx++ )
				{
					var chunkIndex = new ChunkIndex( new Vector3Int( cx, cy, cz ), 0 );

					if ( !_chunks.TryGetValue( chunkIndex, out var chunk ) ) continue;

					var offset = chunkIndex.Min;

					chunk.Subtract( new Capsule( localCapsule.CenterA - offset, localCapsule.CenterB - offset, localCapsule.Radius ) );
					Scene.GetSystem<VoxelRenderingSystem>().QueueChunkUpdate( chunk );
				}
			}
		}
	}

	private ChunkIndex GetChunkIndex( Vector3Int localIndex, int level )
	{
		return new ChunkIndex( new Vector3Int(
			GetChunkIndexComponent( localIndex.x, ChunkSize << level ),
			GetChunkIndexComponent( localIndex.y, ChunkSize << level ),
			GetChunkIndexComponent( localIndex.z, ChunkSize << level ) ), level );
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
				chunk = _chunks[index] = new SceneVoxelsObject( Scene.SceneWorld, index.Min * VoxelSize, ChunkSize, index.VoxelScale * VoxelSize );
				chunk.Position = index.Min * VoxelSize;
			}

			if ( chunk.Generate( (Vector3Int)(index.Min * VoxelSize), Seed ) )
			{
				Scene.GetSystem<VoxelRenderingSystem>().QueueChunkUpdate( chunk );
			}
		}

		foreach ( var (index, chunk) in _chunks )
		{
			chunk.RenderingEnabled = !AllSubChunksLoaded( index );
		}
	}

	private bool AllSubChunksLoaded( ChunkIndex index )
	{
		var firstSubChunk = index.FirstSubChunk;

		for ( var i = 0; i < 8; i++ )
		{
			var offset = new Vector3Int( i & 1, (i >> 1) & 1, (i >> 2) & 1 );
			var subChunk = new ChunkIndex( firstSubChunk.Index + offset, firstSubChunk.Level );

			if ( !_chunks.TryGetValue( subChunk, out var chunk ) ) return false;
			if ( !chunk.IsReady ) return false;
		}

		return true;
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

		var minIndex = origin.Index - loadRadius;
		var maxIndex = origin.Index + loadRadius;

		for ( var z = minIndex.z; z <= maxIndex.z; z++ )
		{
			for ( var y = minIndex.y; y <= maxIndex.y; y++ )
			{
				for ( var x = minIndex.x; x <= maxIndex.x; x++ )
				{
					var index = new Vector3Int( x, y, z );

					if ( (index - origin.Index).LengthSquared <= loadRadius * loadRadius )
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
			chunk?.Delete();
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
