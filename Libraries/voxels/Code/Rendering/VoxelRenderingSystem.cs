using Sandbox;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Voxels.Rendering;

public sealed partial class VoxelRenderingSystem : GameObjectSystem<VoxelRenderingSystem>
{
	private readonly HashSet<SceneVoxelsObject> _dirtyChunkSet = new( ReferenceEqualityComparer.Instance );
	private readonly Queue<SceneVoxelsObject> _dirtyChunkQueue = new();

	private enum VoxelEdge : uint
	{
		AB = 0,
		AC = 1,
		AE = 2
	}

	private readonly record struct VertexData( Vector3Int Origin, VoxelEdge Edge );
	private readonly record struct TriangleData( VertexData A, VertexData B, VertexData C );

	private readonly record struct MarchingCubesLookupEntry(
		uint TriangleCount,
		TriangleData Tri0,
		TriangleData Tri1,
		TriangleData Tri2,
		TriangleData Tri3,
		TriangleData Tri4 )
	{
		public MarchingCubesLookupEntry( params TriangleData[] triangles )
			: this( (uint)triangles.Length,
				triangles.Length > 0 ? triangles[0] : default,
				triangles.Length > 1 ? triangles[1] : default,
				triangles.Length > 2 ? triangles[2] : default,
				triangles.Length > 3 ? triangles[3] : default,
				triangles.Length > 4 ? triangles[4] : default )
		{

		}
	}

	private ComputeShader? _findVerticesCompute;
	private ComputeShader? _findIndicesCompute;
	private GpuBuffer<RenderVertex>? _vertexBuffer;
	private GpuBuffer<uint>? _vertexIndexMap;
	private GpuBuffer<uint>? _indexBuffer;
	private GpuBuffer<uint>? _resultBuffer;

	private uint[]? _resultArray;

	public VoxelRenderingSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, UpdateChunks, "VoxelRenderingSystem.UpdateChunks" );
	}

	public void QueueChunkUpdate( SceneVoxelsObject chunk )
	{
		if ( !_dirtyChunkSet.Add( chunk ) ) return;

		_dirtyChunkQueue.Enqueue( chunk );
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

	private readonly List<(SceneVoxelsObject Chunk, uint VertexOffset, uint IndexOffset)> _updatingChunks = new();

	private static void PrepareBuffer<T>( [NotNull] ref GpuBuffer<T>? buffer, uint elementCount )
		where T : unmanaged
	{
		if ( buffer is not null && buffer.ElementCount >= elementCount ) return;

		buffer?.Dispose();
		buffer = new GpuBuffer<T>( ((int)elementCount).NextPowerOf2 );
	}

	private void UpdateChunks()
	{
		const int maxParallelChunks = 16;

		var maxTotalVertices = 0U;
		var maxTotalIndices = 0U;

		_updatingChunks.Clear();

		while ( _updatingChunks.Count < maxParallelChunks && _dirtyChunkQueue.TryDequeue( out var chunk ) )
		{
			_dirtyChunkSet.Remove( chunk );

			if ( !chunk.IsValid() ) continue;
			if ( chunk.Size == default ) continue;

			chunk.BeforeUpdate();

			if ( chunk.VoxelBuffer is null ) continue;

			_updatingChunks.Add( (chunk, maxTotalVertices, maxTotalIndices) );

			var maxVertices = GetMaxVertexCount( chunk.Size );
			var maxTriangles = GetMaxTriangleCount( chunk.Size );

			maxTotalVertices = (uint)(maxTotalVertices + maxVertices);
			maxTotalIndices = (uint)(maxTotalIndices + maxTriangles * 3);
		}

		if ( _updatingChunks.Count == 0 ) return;

		PrepareBuffer( ref _vertexBuffer, maxTotalVertices );
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
		_findVerticesCompute.Attributes.Set( "VertexIndexMap", _vertexIndexMap );
		_findVerticesCompute.Attributes.Set( "ResultBuffer", _resultBuffer );

		for ( var i = 0; i < _updatingChunks.Count; i++ )
		{
			var (chunk, vertexOffset, indexOffset) = _updatingChunks[i];

			_findVerticesCompute.Attributes.Set( "VoxelData", chunk.VoxelBuffer );
			_findVerticesCompute.Attributes.Set( "VoxelCount", chunk.SizeWithMargin );
			_findVerticesCompute.Attributes.Set( "VoxelOffset", chunk.Offset );
			_findVerticesCompute.Attributes.Set( "VoxelStride", chunk.Stride );

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
			var (chunk, vertexOffset, indexOffset) = _updatingChunks[i];

			_findIndicesCompute.Attributes.Set( "VoxelData", chunk.VoxelBuffer );
			_findIndicesCompute.Attributes.Set( "VoxelCount", chunk.SizeWithMargin );
			_findIndicesCompute.Attributes.Set( "VoxelOffset", chunk.Offset );
			_findIndicesCompute.Attributes.Set( "VoxelStride", chunk.Stride );

			_findIndicesCompute.Attributes.Set( "VertexBufferOffset", vertexOffset );
			_findIndicesCompute.Attributes.Set( "IndexBufferOffset", indexOffset );
			_findIndicesCompute.Attributes.Set( "ResultBufferOffset", i * 2 + 1 );
			_findIndicesCompute.Attributes.Set( "VertexIndexMapStride", new Vector2Int( chunk.Size.x + 1, (chunk.Size.x + 1) * (chunk.Size.y + 1) ) );

			_findIndicesCompute.Dispatch( chunk.Size.x, chunk.Size.y, chunk.Size.z );
		}

		//
		// Copy vertices / indices to each chunk's buffers
		//

		_resultBuffer.GetData( _resultArray );

		for ( var i = 0; i < _updatingChunks.Count; i++ )
		{
			var (chunk, vertexOffset, indexOffset) = _updatingChunks[i];
			var (vertexCount, indexCount) = (_resultArray[i * 2], _resultArray[i * 2 + 1]);

			if ( vertexCount == 0 )
			{
				chunk.ClearMesh();
				continue;
			}

			var (vertexBuffer, indexBuffer) = chunk.PrepareRenderBuffers( vertexCount, indexCount );

			_vertexBuffer.CopyTo( vertexBuffer, (int)vertexOffset, 0, (int)vertexCount );
			_indexBuffer.CopyTo( indexBuffer, (int)indexOffset, 0, (int)indexCount );
		}
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
