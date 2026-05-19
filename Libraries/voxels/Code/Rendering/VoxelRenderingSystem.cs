using System;
using Sandbox;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Voxels.Rendering;

public sealed partial class VoxelRenderingSystem : GameObjectSystem<VoxelRenderingSystem>
{
	private readonly HashSet<(SceneVoxelsObject Chunk, uint Generation)> _dirtyChunkSet = new();
	private readonly Queue<(SceneVoxelsObject Chunk, uint Generation)> _dirtyChunkQueue = new();

	private ComputeShader? _findVerticesCompute;
	private ComputeShader? _findIndicesCompute;
	private GpuBuffer<RenderVertex>? _vertexBuffer;
	private GpuBuffer<Vector3>? _physicsVertexBuffer;
	private GpuBuffer<uint>? _vertexIndexMap;
	private GpuBuffer<uint>? _indexBuffer;
	private GpuBuffer<uint>? _resultBuffer;

	private enum UpdateState
	{
		None,
		StartingJobs,
		WaitingForResult,
		CopyingBuffers
	}

	private uint[]? _resultArray;
	private UpdateState _updateState;

	public VoxelRenderingSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, UpdateChunks, "VoxelRenderingSystem.UpdateChunks" );
	}

	public void QueueChunkUpdate( SceneVoxelsObject chunk )
	{
		var generation = chunk.Generation;

		if ( !_dirtyChunkSet.Add( (chunk, generation) ) ) return;

		_dirtyChunkQueue.Enqueue( (chunk, generation) );
	}

	private static int GetMaxVertexCount( Vector3Int chunkSize )
	{
		chunkSize += 1;

		return chunkSize.x * chunkSize.y * chunkSize.z * 3;
	}

	private static int GetMaxTriangleCount( Vector3Int chunkSize )
	{
		return chunkSize.x * chunkSize.y * chunkSize.z * 5;
	}

	private readonly List<(SceneVoxelsObject Chunk, uint Generation, uint VertexOffset, uint IndexOffset)> _updatingChunks = new();

	private static void PrepareBuffer<T>( [NotNull] ref GpuBuffer<T>? buffer, uint elementCount )
		where T : unmanaged
	{
		if ( buffer is not null && buffer.ElementCount >= elementCount ) return;

		buffer?.Dispose();
		buffer = new GpuBuffer<T>( ((int)elementCount).NextPowerOf2 );
	}

	private void UpdateChunks()
	{
		if ( _updateState == UpdateState.StartingJobs )
		{
			if ( _updatingChunks.TrueForAll( x => !x.Chunk.IsValid() || x.Chunk.Generation != x.Generation ) )
			{
				_updateState = UpdateState.None;
				_updatingChunks.Clear();
			}
		}

		if ( _updateState == UpdateState.CopyingBuffers )
		{
			for ( var i = 0; i < _updatingChunks.Count; i++ )
			{
				var (chunk, generation, vertexOffset, indexOffset) = _updatingChunks[i];

				if ( chunk.Generation != generation ) continue;

				var (vertexCount, indexCount) = (_resultArray![i * 2], _resultArray[i * 2 + 1]);

				if ( vertexCount == 0 )
				{
					chunk.ClearMesh();
					continue;
				}

				var (vertexBuffer, physicsVertexBuffer, indexBuffer) = chunk.PrepareRenderBuffers( vertexCount, indexCount );

				_vertexBuffer!.CopyTo( vertexBuffer, (int)vertexOffset, 0, (int)vertexCount );
				_indexBuffer!.CopyTo( indexBuffer, (int)indexOffset, 0, (int)indexCount );

				if ( physicsVertexBuffer is not null )
				{
					_physicsVertexBuffer!.CopyTo( physicsVertexBuffer, (int)vertexOffset, 0, (int)vertexCount );
				}
			}

			_updateState = UpdateState.None;
			_updatingChunks.Clear();
		}

		if ( _updateState != UpdateState.None ) return;

		const int maxParallelChunks = 16;

		var maxTotalVertices = 0U;
		var maxTotalIndices = 0U;

		while ( _updatingChunks.Count < maxParallelChunks && _dirtyChunkQueue.TryDequeue( out var next ) )
		{
			_dirtyChunkSet.Remove( next );

			if ( !next.Chunk.IsValid() ) continue;
			if ( next.Chunk.Generation != next.Generation ) continue;
			if ( next.Chunk.Size == default ) continue;

			next.Chunk.BeforeUpdate();

			if ( next.Chunk.VoxelBuffer is null ) continue;

			_updatingChunks.Add( (next.Chunk, next.Generation, maxTotalVertices, maxTotalIndices) );

			var maxVertices = GetMaxVertexCount( next.Chunk.Size );
			var maxTriangles = GetMaxTriangleCount( next.Chunk.Size );

			maxTotalVertices = (uint)(maxTotalVertices + maxVertices);
			maxTotalIndices = (uint)(maxTotalIndices + maxTriangles * 3);
		}

		if ( _updatingChunks.Count == 0 ) return;

		_updateState = UpdateState.StartingJobs;

		PrepareBuffer( ref _vertexBuffer, maxTotalVertices );
		PrepareBuffer( ref _physicsVertexBuffer, maxTotalVertices );
		PrepareBuffer( ref _vertexIndexMap, maxTotalVertices );
		PrepareBuffer( ref _indexBuffer, maxTotalIndices );
		PrepareBuffer( ref _resultBuffer, maxParallelChunks * 2 );

		if ( _resultArray is null || _resultArray.Length < _resultBuffer.ElementCount )
		{
			_resultArray = new uint[_resultBuffer.ElementCount];
		}

		_vertexBuffer.Clear();
		_indexBuffer.Clear();
		_vertexIndexMap.Clear();
		_resultBuffer.Clear();

		//
		// Find vertices
		//

		_findVerticesCompute ??= new ComputeShader( "Shaders/marching_cubes/find_vertices_cs.shader" );

		_findVerticesCompute.Attributes.Set( "VertexBuffer", _vertexBuffer );
		_findVerticesCompute.Attributes.Set( "PhysicsVertexBuffer", _physicsVertexBuffer );
		_findVerticesCompute.Attributes.Set( "VertexIndexMap", _vertexIndexMap );
		_findVerticesCompute.Attributes.Set( "ResultBuffer", _resultBuffer );

		for ( var i = 0; i < _updatingChunks.Count; i++ )
		{
			var (chunk, _, vertexOffset, _) = _updatingChunks[i];

			_findVerticesCompute.Attributes.Set( "VoxelData", chunk.VoxelBuffer );
			_findVerticesCompute.Attributes.Set( "VoxelCount", chunk.SizeWithMargin );
			_findVerticesCompute.Attributes.Set( "VoxelOffset", chunk.Offset );
			_findVerticesCompute.Attributes.Set( "VoxelStride", chunk.Stride );
			_findVerticesCompute.Attributes.Set( "VoxelScale", chunk.VoxelScale );

			_findVerticesCompute.Attributes.Set( "VertexBufferOffset", vertexOffset );
			_findVerticesCompute.Attributes.Set( "ResultBufferOffset", i * 2 );
			_findVerticesCompute.Attributes.Set( "VertexIndexMapStride", new Vector2Int( chunk.Size.x + 1, (chunk.Size.x + 1) * (chunk.Size.y + 1) ) );

			_findVerticesCompute.Dispatch( chunk.Size.x + 1, chunk.Size.y + 1, chunk.Size.z + 1 );
		}

		//
		// Find indices
		//

		_findIndicesCompute ??= new ComputeShader( "Shaders/marching_cubes/find_indices_cs.shader" );

		_findIndicesCompute.Attributes.Set( "MarchingCubesLookup", GenerateMarchingCubesLookupTable() );
		_findIndicesCompute.Attributes.Set( "IndexBuffer", _indexBuffer );
		_findIndicesCompute.Attributes.Set( "VertexIndexMap", _vertexIndexMap );
		_findIndicesCompute.Attributes.Set( "ResultBuffer", _resultBuffer );

		for ( var i = 0; i < _updatingChunks.Count; i++ )
		{
			var (chunk, _, vertexOffset, indexOffset) = _updatingChunks[i];

			_findIndicesCompute.Attributes.Set( "VoxelData", chunk.VoxelBuffer );
			_findIndicesCompute.Attributes.Set( "VoxelCount", chunk.SizeWithMargin );
			_findIndicesCompute.Attributes.Set( "VoxelOffset", chunk.Offset );
			_findIndicesCompute.Attributes.Set( "VoxelStride", chunk.Stride );

			_findIndicesCompute.Attributes.Set( "VertexBufferOffset", vertexOffset );
			_findIndicesCompute.Attributes.Set( "IndexBufferOffset", indexOffset );
			_findIndicesCompute.Attributes.Set( "ResultBufferOffset", i * 2 + 1 );
			_findIndicesCompute.Attributes.Set( "VertexIndexMapStride", new Vector2Int( chunk.Size.x + 1, (chunk.Size.x + 1) * (chunk.Size.y + 1) ) );

			_findIndicesCompute.Dispatch( chunk.Size.x, chunk.Size.y, chunk.Size.z );

			chunk.BeforeRenderAction = BeforeUpdatingChunkRender;
		}
	}

	private void BeforeUpdatingChunkRender()
	{
		// gets called on the render thread by an updating chunk,
		// hack to be able to call GetDataAsync

		if ( _updatingChunks.Count == 0 ) return;
		if ( _updateState != UpdateState.StartingJobs ) return;

		_updateState = UpdateState.WaitingForResult;

		_resultBuffer!.GetDataAsync( result =>
		{
			result.CopyTo( _resultArray );
			_updateState = UpdateState.CopyingBuffers;
		} );
	}
}

internal static class MathExtensions
{
	extension( int value )
	{
		public int NextPowerOf2
		{
			get
			{
				var po2 = 1;

				while ( po2 < value )
				{
					po2 <<= 1;
				}

				return po2;
			}
		}
	}
}
