using Sandbox;
using System.Collections.Generic;
using System.Linq;

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
	private GpuBuffer<uint>? _countBuffer;

	private uint[]? _countArray;

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

	private void UpdateChunks()
	{
		const int maxParallelChunks = 1;

		var maxTotalVertices = 0U;
		var maxTotalIndices = 0U;

		_updatingChunks.Clear();

		while ( _updatingChunks.Count < maxParallelChunks && _dirtyChunkQueue.TryDequeue( out var chunk ) )
		{
			_dirtyChunkSet.Remove( chunk );

			if ( !chunk.IsValid() ) continue;
			if ( chunk.Size == default ) continue;
			if ( chunk.VoxelBuffer is null ) continue;

			_updatingChunks.Add( (chunk, maxTotalVertices, maxTotalIndices) );

			var maxVertices = GetMaxVertexCount( chunk.Size );
			var maxTriangles = GetMaxTriangleCount( chunk.Size );

			maxTotalVertices = (uint)(maxTotalVertices + maxVertices);
			maxTotalIndices = (uint)(maxTotalIndices + maxTriangles * 3);
		}

		if ( _updatingChunks.Count == 0 ) return;

		if ( _vertexBuffer is null || _vertexBuffer.ElementCount < maxTotalVertices )
		{
			_vertexBuffer?.Dispose();
			_vertexBuffer = new GpuBuffer<RenderVertex>( ((int)maxTotalVertices).NextPowerOf2 );
		}

		if ( _vertexIndexMap is null || _vertexIndexMap.ElementCount < maxTotalVertices )
		{
			_vertexIndexMap?.Dispose();
			_vertexIndexMap = new GpuBuffer<uint>( ((int)maxTotalVertices).NextPowerOf2 );
		}

		if ( _indexBuffer is null || _indexBuffer.ElementCount < maxTotalIndices )
		{
			_indexBuffer?.Dispose();
			_indexBuffer = new GpuBuffer<uint>( ((int)maxTotalIndices).NextPowerOf2 );
		}

		if ( _countBuffer is null || _countBuffer.ElementCount < maxParallelChunks * 2 )
		{
			_countBuffer?.Dispose();
			_countBuffer = new GpuBuffer<uint>( maxParallelChunks * 2 );
		}

		_countBuffer.Clear();

		_findVerticesCompute ??= new ComputeShader( "Shaders/marching_cubes/find_vertices_cs.shader" );

		_findVerticesCompute.Attributes.Set( "VertexBuffer", _vertexBuffer );
		_findVerticesCompute.Attributes.Set( "VertexIndexMap", _vertexIndexMap );

		//if ( _marchingCubesCompute is null )
		//{
		//	_marchingCubesCompute = new ComputeShader( "Shaders/marching_cubes/find_triangles_cs.shader" );
		//	_marchingCubesCompute.Attributes.Set( "MarchingCubesLookup", GenerateMarchingCubesLookupTable() );
		//}

		//_marchingCubesCompute.Attributes.Set( "TriangleBuffer", _triangleBuffer );
		//_marchingCubesCompute.Attributes.Set( "TriangleCount", _countBuffer );

		//for ( var i = 0; i < _updatingChunks.Count; i++ )
		//{
		//	var chunk = _updatingChunks[i];

		//	_marchingCubesCompute.Attributes.Set( "ChunkIndex", i );
		//	_marchingCubesCompute.Attributes.Set( "VoxelData", chunk.VoxelBuffer! );
		//	_marchingCubesCompute.Attributes.Set( "VoxelOffset", chunk.Offset );
		//	_marchingCubesCompute.Attributes.Set( "VoxelStride", chunk.Stride );

		//	_marchingCubesCompute.Dispatch( chunk.Size.x, chunk.Size.y, chunk.Size.z );
		//}

		//if ( _countArray is null || _countArray.Length < maxParallelChunks )
		//{
		//	_countArray = new uint[maxParallelChunks];
		//}

		//_countBuffer.GetData( _countArray );

		//for ( var i = 0; i < _updatingChunks.Count; i++ )
		//{
		//	var chunk = _updatingChunks[i];
		//	var firstIndex = _firstIndices[i];
		//	var faceCount = (int)(_countArray[i] - firstIndex);

		//	Log.Info( $"faceCount: {faceCount}" );
		//}

		//_buildMeshCompute ??= new ComputeShader( "Shaders/voxels/cubes/build_mesh_cs.shader" );

		//_buildMeshCompute.Attributes.Set( "FaceBuffer", _faceBuffer );

		//for ( var i = 0; i < _updatingChunks.Count; i++ )
		//{
		//	var chunk = _updatingChunks[i];
		//	var firstIndex = _firstIndices[i];
		//	var faceCount = (int)(_countArray[i] - firstIndex);

		//	if ( faceCount == 0 )
		//	{
		//		chunk.ClearMesh();
		//		continue;
		//	}

		//	var buffers = chunk.PrepareMeshBuffers( faceCount );

		//	_buildMeshCompute.Attributes.Set( "FirstFaceIndex", (int)firstIndex );
		//	_buildMeshCompute.Attributes.Set( "VertexBuffer", buffers.Vertex );
		//	_buildMeshCompute.Attributes.Set( "IndexBuffer", buffers.Index );

		//	_buildMeshCompute.Dispatch( faceCount, 1, 1 );
		//}
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
