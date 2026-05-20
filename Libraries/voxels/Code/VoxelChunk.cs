using Sandbox;
using System;
using Voxels.Rendering;

namespace Voxels;

internal readonly record struct GpuVoxelSpan(
	GpuBuffer<uint> Buffer,
	Vector3Int Offset,
	Vector3Int Size,
	Vector3Int Stride );

internal sealed class VoxelChunk : IDisposable, IValid
{
	public const int Size = 32;
	public const int Margin = 1;

	public VoxelVolume Volume { get; }
	public ChunkIndex Index { get; }
	public float VoxelScale { get; }

	public bool IsValid { get; private set; }

	private readonly GpuBuffer<uint> _buffer;

	private SceneVoxelsObject? _sceneObject;

	internal byte LodMask { get; set; }
	internal float LodDistance { get; set; }

	internal GpuVoxelSpan FullVoxelSpan { get; }

	internal GpuVoxelSpan VisibleVoxelSpan { get; }

	internal VoxelRenderMesh? RenderMesh
	{
		get;
		set
		{
			field = value;
			IsReady = true;

			if ( value is null )
			{
				_sceneObject?.Delete();
				_sceneObject = null;
			}
			else if ( _sceneObject is null)
			{
				_sceneObject = new SceneVoxelsObject( this );
				_sceneObject.Initialize( Index.Min * Volume.VoxelSize, Size, VoxelScale );
			}
		}
	}

	public bool IsReady { get; private set; }
	public bool IsPhysicsReady { get; private set; }

	public VoxelChunk( VoxelVolume volume, ChunkIndex index, float scale )
	{
		Volume = volume;
		Index = index;
		VoxelScale = scale;

		var sizeWithMargin = Size + Margin * 2 + 1;
		var stride = new Vector3Int(
			sizeWithMargin,
			sizeWithMargin * sizeWithMargin,
			sizeWithMargin * sizeWithMargin * sizeWithMargin );

		_buffer = new GpuBuffer<uint>( stride.z );

		FullVoxelSpan = new GpuVoxelSpan( _buffer, 0, sizeWithMargin, stride );
		VisibleVoxelSpan = new GpuVoxelSpan( _buffer, 1, Size, stride );

		IsValid = true;
	}

	private static ComputeShader? _generateCompute;

	public void Generate( Vector3 worldOffset, int seed )
	{
		_generateCompute ??= new ComputeShader( "Shaders/procgen/caveworld.shader" );

		_generateCompute.Attributes.Set( "VoxelData", _buffer );
		_generateCompute.Attributes.Set( "VoxelCount", FullVoxelSpan.Size );
		_generateCompute.Attributes.Set( "VoxelOffset", FullVoxelSpan.Offset );
		_generateCompute.Attributes.Set( "VoxelStride", FullVoxelSpan.Stride );
		_generateCompute.Attributes.Set( "VoxelScale", VoxelScale );

		var random = new Random( seed );
		var seedOffset = new Vector3Int( random.Next( -32768, 32768 ), random.Next( -32768, 32768 ), 0 );

		_generateCompute.Attributes.Set( "WorldOrigin", worldOffset + seedOffset - Margin * VoxelScale );

		_generateCompute.Dispatch( FullVoxelSpan.Size.x, FullVoxelSpan.Size.y, 1 );

		Volume.Scene.Get<VoxelRenderingSystem>().QueueChunkUpdate( this );
	}

	public void Dispose()
	{
		IsValid = false;

		_buffer.Dispose();

		RenderMesh?.Dispose();
		RenderMesh = null;

		_sceneObject?.Delete();
		_sceneObject = null;
	}
}
