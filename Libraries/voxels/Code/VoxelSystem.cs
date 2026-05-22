using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Voxels.Modification;
using Voxels.Physics;
using Voxels.Rendering;
using static Sandbox.Volumes.VolumeSystem;

namespace Voxels;

internal sealed partial class VoxelSystem : GameObjectSystem<VoxelSystem>
{
	private readonly HashSet<VoxelChunk> _dirtyChunkSet = new();
	private readonly Queue<VoxelChunk> _dirtyChunkQueue = new();

	private static ComputeShader? _modificationCompute;
	private ComputeShader? _findVerticesCompute;
	private ComputeShader? _findIndicesCompute;
	private static GpuBuffer<VoxelModificationEntry>? _modificationBuffer;
	private static GpuBuffer<uint>? _parameterBuffer;
	private GpuBuffer<RenderVertex>? _vertexBuffer;
	private GpuBuffer<Vector3>? _physicsVertexBuffer;
	private GpuBuffer<uint>? _vertexIndexMap;
	private GpuBuffer<uint>? _indexBuffer;
	private GpuBuffer<uint>? _resultBuffer;

	private readonly List<VoxelModificationEntry> _modifications = new();
	private readonly List<uint> _modificationParameters = new();
	private readonly List<uint> _resultList = new();
	private readonly SceneCustomObject _sceneObject;

	public VoxelSystem( Scene scene ) : base( scene )
	{
		_sceneObject = new SceneDummyObject( this );

		Listen( Stage.FinishUpdate, 0, UpdateChunks, "VoxelRenderingSystem.UpdateChunks" );
	}

	public override void Dispose()
	{
		base.Dispose();

		_sceneObject.Delete();
	}

	internal void QueueChunkUpdate( VoxelChunk chunk )
	{
		if ( _dirtyChunkSet.Add( chunk ) )
		{
			_dirtyChunkQueue.Enqueue( chunk );
		}
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

	private static void PrepareBuffer<T>( [NotNull] ref GpuBuffer<T>? buffer, uint elementCount )
		where T : unmanaged
	{
		if ( buffer is not null && buffer.ElementCount >= elementCount ) return;

		buffer?.Dispose();
		buffer = new GpuBuffer<T>( ((int)elementCount).NextPowerOf2 );
	}

	private Task? _updateChunksTask;

	private void UpdateChunks()
	{
		if ( _dirtyChunkQueue.Count > 0 && _updateChunksTask is null or { IsCompleted: true } )
		{
			_updateChunksTask = UpdateChunksAsync();
		}
	}

	private readonly record struct UpdatingChunk( VoxelChunk Chunk, uint ModificationOffset, uint ModificationCount, uint VertexOffset, uint IndexOffset );

	private readonly List<UpdatingChunk> _updatingChunks = new();
	private readonly List<Task> _pendingTasks = new();

	private async Task UpdateChunksAsync()
	{
		const int maxParallelChunks = 16;

		_updatingChunks.Clear();
		_modifications.Clear();
		_modificationParameters.Clear();

		var maxTotalVertices = 0U;
		var maxTotalIndices = 0U;

		while ( _updatingChunks.Count < maxParallelChunks && _dirtyChunkQueue.TryDequeue( out var next ) )
		{
			_dirtyChunkSet.Remove( next );

			if ( !next.IsValid() ) continue;

			var maxVertices = GetMaxVertexCount( VoxelChunk.Size );
			var maxTriangles = GetMaxTriangleCount( VoxelChunk.Size );

			maxTotalVertices = (uint)(maxTotalVertices + maxVertices);
			maxTotalIndices = (uint)(maxTotalIndices + maxTriangles * 3);

			var modificationOffset = (uint)_modifications.Count;

			next.WritePendingModifications( _modifications, _modificationParameters );

			var modificationCount = (uint)(_modifications.Count - modificationOffset);

			_updatingChunks.Add( new( next,
				modificationOffset, modificationCount,
				maxTotalVertices, maxTotalIndices ) );
		}

		if ( _updatingChunks.Count == 0 ) return;

		if ( _modifications.Count > 0 )
		{
			PrepareBuffer( ref _modificationBuffer, (uint)_modifications.Count );
			PrepareBuffer( ref _parameterBuffer, (uint)_modificationParameters.Count );

			_modificationCompute ??= new ComputeShader( "Shaders/voxels/modification_cs.shader" );

			_modificationBuffer.SetData( _modifications );
			_parameterBuffer.SetData( _modificationParameters );

			_modificationCompute.Attributes.Set( "ModificationList", _modificationBuffer );
			_modificationCompute.Attributes.Set( "ParameterData", _parameterBuffer );

			foreach ( var next in _updatingChunks )
			{
				if ( next.ModificationCount == 0 ) continue;

				var span = next.Chunk.FullVoxelSpan;

				_modificationCompute.Attributes.Set( "VoxelData", span.Buffer );
				_modificationCompute.Attributes.Set( "VoxelCount", span.Size );
				_modificationCompute.Attributes.Set( "VoxelOffset", span.Offset );
				_modificationCompute.Attributes.Set( "VoxelStride", span.Stride );

				var worldOrigin = next.Chunk.Index.Min * next.Chunk.Volume.VoxelSize - VoxelChunk.Margin * next.Chunk.VoxelScale;

				_modificationCompute.Attributes.Set( "VoxelScale", next.Chunk.VoxelScale );
				_modificationCompute.Attributes.Set( "WorldOrigin", worldOrigin );

				_modificationCompute.Attributes.Set( "ModificationOffset", next.ModificationOffset );
				_modificationCompute.Attributes.Set( "ModificationCount", next.ModificationCount );

				_modificationCompute.Dispatch( span.Size.x, span.Size.y, 1 );
			}
		}

		PrepareBuffer( ref _vertexBuffer, maxTotalVertices );
		PrepareBuffer( ref _physicsVertexBuffer, maxTotalVertices );
		PrepareBuffer( ref _vertexIndexMap, maxTotalVertices );
		PrepareBuffer( ref _indexBuffer, maxTotalIndices );
		PrepareBuffer( ref _resultBuffer, maxParallelChunks * 2 );

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
			var (chunk, _, _, vertexOffset, _) = _updatingChunks[i];
			var voxelSpan = chunk.VisibleVoxelSpan;

			_findVerticesCompute.Attributes.Set( "VoxelData", voxelSpan.Buffer );
			_findVerticesCompute.Attributes.Set( "VoxelOffset", voxelSpan.Offset );
			_findVerticesCompute.Attributes.Set( "VoxelStride", voxelSpan.Stride );
			_findVerticesCompute.Attributes.Set( "VoxelScale", chunk.VoxelScale );

			var vertexCount = voxelSpan.Size + 1;
			var vertexStride = new Vector3Int(
				vertexCount.x,
				vertexCount.x * vertexCount.y,
				vertexCount.x * vertexCount.y * vertexCount.z );

			_findVerticesCompute.Attributes.Set( "VertexBufferOffset", vertexOffset );
			_findVerticesCompute.Attributes.Set( "ResultBufferOffset", i * 2 );
			_findVerticesCompute.Attributes.Set( "VertexIndexMapStride", vertexStride );

			_findVerticesCompute.Dispatch( vertexCount.x, vertexCount.y, vertexCount.z );
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
			var (chunk, _, _, vertexOffset, indexOffset) = _updatingChunks[i];
			var voxelSpan = chunk.VisibleVoxelSpan;

			_findIndicesCompute.Attributes.Set( "VoxelData", voxelSpan.Buffer );
			_findIndicesCompute.Attributes.Set( "VoxelOffset", voxelSpan.Offset );
			_findIndicesCompute.Attributes.Set( "VoxelStride", voxelSpan.Stride );

			var vertexCount = voxelSpan.Size + 1;
			var vertexStride = new Vector3Int(
				vertexCount.x,
				vertexCount.x * vertexCount.y,
				vertexCount.x * vertexCount.y * vertexCount.z );

			_findIndicesCompute.Attributes.Set( "VertexBufferOffset", vertexOffset );
			_findIndicesCompute.Attributes.Set( "IndexBufferOffset", indexOffset );
			_findIndicesCompute.Attributes.Set( "ResultBufferOffset", i * 2 + 1 );
			_findIndicesCompute.Attributes.Set( "VertexIndexMapStride", vertexStride );

			_findIndicesCompute.Dispatch( voxelSpan.Size.x, voxelSpan.Size.y, voxelSpan.Size.z );
		}

		//
		// Download vertex / index counts
		//

		_resultList.Clear();

		await GetDataAsync( _resultBuffer, result => _resultList.AddRange( result ), 0, _updatingChunks.Count * 2 );

		_pendingTasks.Clear();

		//
		// Submit results
		//

		for ( var i = 0; i < _updatingChunks.Count; i++ )
		{
			var (chunk, _, _, vertexOffset, indexOffset) = _updatingChunks[i];
			var (vertexCount, indexCount) = (_resultList[i * 2], _resultList[i * 2 + 1]);

			if ( !chunk.IsValid ) continue;

			chunk.RenderMesh = vertexCount > 0 && indexCount > 0
				? new VoxelRenderMesh(
					_vertexBuffer, (int)vertexOffset, (int)vertexCount,
					_indexBuffer, (int)indexOffset, (int)indexCount )
				: null;

			if ( chunk.Index.Level > 0 ) continue;

			if ( vertexCount == 0 || indexCount == 0 )
			{
				chunk.CollisionMesh = null;
			}
			else
			{
				_pendingTasks.Add( UpdateCollisionMesh( chunk,
					(int)vertexOffset, (int)vertexCount,
					(int)indexOffset, (int)indexCount ) );
			}
		}

		await GameTask.WhenAll( _pendingTasks );

		foreach ( var item in _updatingChunks )
		{
			if ( item.Chunk.HasPendingModifications )
			{
				QueueChunkUpdate( item.Chunk );
			}
		}
	}

	private async Task UpdateCollisionMesh( VoxelChunk chunk, int vertexOffset, int vertexCount, int indexOffset, int indexCount )
	{
		var mesh = new VoxelCollisionMesh();

		var updateVerticesTask = GetDataAsync( _physicsVertexBuffer!, mesh.UpdateVertices, vertexOffset, vertexCount );
		var updateIndicesTask = GetDataAsync<uint, int>( _indexBuffer!, mesh.UpdateIndices, indexOffset, indexCount );

		await GameTask.WhenAll( updateVerticesTask, updateIndicesTask );

		if ( !chunk.IsValid ) return;

		chunk.CollisionMesh = mesh;
	}

	internal Task GetDataAsync<T>( GpuBuffer<T> buffer, Action<ReadOnlySpan<T>> action, int srcOffset, int srcCount )
		where T : unmanaged
	{
		return GetDataAsync<T, T>( buffer, action, srcOffset, srcCount );
	}

	internal Task GetDataAsync<T1, T2>( GpuBuffer<T1> buffer, Action<ReadOnlySpan<T2>> action, int srcOffset, int srcCount )
		where T1 : unmanaged
		where T2 : unmanaged
	{
		var tcs = new TaskCompletionSource();

		_renderThreadActions.Enqueue( () =>
		{
			try
			{
				buffer.GetDataAsync<T2>( result =>
				{
					try
					{
						action( result );
						tcs.SetResult();
					}
					catch ( Exception ex )
					{
						tcs.SetException( ex );
					}
				}, srcOffset, srcCount );
			}
			catch ( Exception ex )
			{
				Log.Info( $"GetDataAsync( {buffer.ElementCount}, {srcOffset}, {srcCount} )" );
				tcs.SetException( ex );
			}
		} );

		return tcs.Task;
	}

	private readonly ConcurrentQueue<Action> _renderThreadActions = new();

	internal void RenderThreadTick()
	{
		while ( _renderThreadActions.TryDequeue( out var task ) )
		{
			task.InvokeWithWarning();
		}
	}
}

file sealed class SceneDummyObject : SceneCustomObject
{
	private readonly VoxelSystem _voxelSystem;

	public SceneDummyObject( VoxelSystem voxelSystem )
		: base( voxelSystem.Scene.SceneWorld )
	{
		_voxelSystem = voxelSystem;
	}

	public override void RenderSceneObject()
	{
		_voxelSystem.RenderThreadTick();
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
