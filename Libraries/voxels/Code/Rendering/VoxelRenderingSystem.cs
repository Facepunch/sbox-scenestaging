using System;
using Sandbox;
using System.Collections.Generic;

namespace Voxels.Rendering;

public sealed class VoxelRenderingSystem : GameObjectSystem<VoxelRenderingSystem>
{
	private readonly HashSet<SceneCubicVoxelsObject> _dirtyChunkSet = new( ReferenceEqualityComparer.Instance );
	private readonly Queue<SceneCubicVoxelsObject> _dirtyChunkQueue = new();

	private ComputeShader? _appendFacesCompute;
	private ComputeShader? _buildMeshCompute;
	private GpuBuffer<CubeFace>? _faceBuffer;
	private GpuBuffer<uint>? _countBuffer;

	private uint[]? _countArray;

	public VoxelRenderingSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, UpdateChunks, "VoxelRenderingSystem.UpdateChunks" );
	}

	public void QueueChunkUpdate( SceneCubicVoxelsObject chunk )
	{
		if ( !_dirtyChunkSet.Add( chunk ) ) return;

		_dirtyChunkQueue.Enqueue( chunk );
	}

	private static int GetMaxFaceCount( Vector3Int chunkSize )
	{
		return chunkSize.x * chunkSize.y * chunkSize.z * 6;
	}

	private readonly List<SceneCubicVoxelsObject> _updatingChunks = new();
	private readonly List<uint> _firstIndices = new();

	private void UpdateChunks()
	{
		const int maxParallelChunks = 16;

		var maxTotalFaces = 0;

		_updatingChunks.Clear();
		_firstIndices.Clear();

		while ( _updatingChunks.Count < maxParallelChunks && _dirtyChunkQueue.TryDequeue( out var chunk ) )
		{
			_dirtyChunkSet.Remove( chunk );

			if ( !chunk.IsValid() ) continue;
			if ( chunk.Size == default ) continue;
			if ( chunk.VoxelBuffer is null ) continue;

			_updatingChunks.Add( chunk );

			_firstIndices.Add( (uint)maxTotalFaces );

			var maxFaces = GetMaxFaceCount( chunk.Size );

			maxTotalFaces += maxFaces;
		}

		if ( _updatingChunks.Count == 0 ) return;

		if ( _faceBuffer is null || _faceBuffer.ElementCount < maxTotalFaces )
		{
			_faceBuffer?.Dispose();
			_faceBuffer = new GpuBuffer<CubeFace>( maxTotalFaces.NextPowerOf2, GpuBuffer.UsageFlags.Structured, "CompressedFaces" );
		}

		if ( _countBuffer is null || _countBuffer.ElementCount < maxParallelChunks )
		{
			_countBuffer ??= new GpuBuffer<uint>( maxParallelChunks );
		}

		_countBuffer.SetData( _firstIndices );

		_appendFacesCompute ??= new ComputeShader( "Shaders/voxels/cubes/append_faces_cs.shader" );

		_appendFacesCompute.Attributes.Set( "FaceBuffer", _faceBuffer );
		_appendFacesCompute.Attributes.Set( "FaceCount", _countBuffer );

		foreach ( var chunk in _updatingChunks )
		{
			_appendFacesCompute.Attributes.Set( "VoxelData", chunk.VoxelBuffer! );
			_appendFacesCompute.Attributes.Set( "VoxelOffset", chunk.Offset );
			_appendFacesCompute.Attributes.Set( "VoxelStride", chunk.Stride );

			_appendFacesCompute.Dispatch( chunk.Size.x, chunk.Size.y, chunk.Size.z );
		}

		if ( _countArray is null || _countArray.Length < maxParallelChunks )
		{
			_countArray = new uint[maxParallelChunks];
		}

		_countBuffer.GetData( _countArray );

		_buildMeshCompute ??= new ComputeShader( "Shaders/voxels/cubes/build_mesh_cs.shader" );

		_buildMeshCompute.Attributes.Set( "FaceBuffer", _faceBuffer );

		for ( var i = 0; i < _updatingChunks.Count; i++ )
		{
			var chunk = _updatingChunks[i];
			var firstIndex = _firstIndices[i];
			var faceCount = (int)(_countArray[i] - firstIndex);

			var buffers = chunk.PrepareMeshBuffers( faceCount );

			_buildMeshCompute.Attributes.Set( "FirstFaceIndex", firstIndex );
			_buildMeshCompute.Attributes.Set( "VertexBuffer", buffers.Vertex );
			_buildMeshCompute.Attributes.Set( "IndexBuffer", buffers.Index );

			_buildMeshCompute.Dispatch( faceCount, 1, 1 );
		}
	}
}

internal readonly record struct CubeVertex(
	[field: VertexLayout.Position] Vector3 Position,
	[field: VertexLayout.Normal] Vector3 Normal,
	[field: VertexLayout.Tangent] Vector4 Tangent,
	[field: VertexLayout.TexCoord] Vector2 TexCoord );

internal readonly record struct CubeFace( Vector3Int Position, int Normal );

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
