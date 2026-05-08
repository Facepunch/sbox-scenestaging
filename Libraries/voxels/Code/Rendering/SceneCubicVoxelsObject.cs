using Sandbox;
using System;

namespace Voxels.Rendering;

public sealed class SceneCubicVoxelsObject : SceneCustomObject
{
	private static Material Material { get; } = Material.FromShader( "Shaders/voxels/cubes.shader" );

	private GpuBuffer<CubeVertex>? _vertexBuffer;
	private GpuBuffer<uint>? _indexBuffer;
	private int _vertexCount;
	private int _indexCount;

	public Vector3Int WorldOrigin
	{
		get;
		set
		{
			field = value;
			Attributes.Set( "WorldOrigin", value );
		}
	}

	public Vector3Int Size { get; private set; }

	internal Vector3Int Offset { get; private set; }
	internal Vector2Int Stride { get; private set; }

	internal GpuBuffer<uint>? VoxelBuffer { get; private set; }

	public SceneCubicVoxelsObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		Flags.CastShadows = true;
		Flags.IsOpaque = true;
	}

	public void Generate( Vector3Int size, Vector3Int offset, int seed )
	{
		var sizeWithMargin = size + 2;
		var voxelCount = sizeWithMargin.x * sizeWithMargin.y * sizeWithMargin.z;

		WorldOrigin = offset;
		Size = size;
		Offset = 1;
		Stride = new Vector2Int( sizeWithMargin.x, sizeWithMargin.x * sizeWithMargin.y );

		if ( VoxelBuffer is null || VoxelBuffer.ElementCount != voxelCount )
		{
			VoxelBuffer?.Dispose();
			VoxelBuffer = new GpuBuffer<uint>( voxelCount );
		}

		VoxelBuffer.Clear();

		var procGenCompute = new ComputeShader( "Shaders/procgen/caveworld.shader" );

		procGenCompute.Attributes.Set( "VoxelData", VoxelBuffer );
		procGenCompute.Attributes.Set( "VoxelSize", sizeWithMargin );
		procGenCompute.Attributes.Set( "VoxelOffset", new Vector3Int( 0, 0, 0 ) );
		procGenCompute.Attributes.Set( "VoxelStride", Stride );

		var random = new Random( seed );
		var seedOffset = new Vector3Int( random.Next( -1024, 1024 ), random.Next( -1024, 1024 ), 0 );

		procGenCompute.Attributes.Set( "WorldOrigin", offset + seedOffset + 1 );

		procGenCompute.Dispatch( sizeWithMargin.x, sizeWithMargin.y, 1 );
	}

	internal void ClearMesh()
	{
		_vertexCount = 0;
		_indexCount = 0;

		_vertexBuffer?.Dispose();
		_indexBuffer?.Dispose();

		_vertexBuffer = null;
		_indexBuffer = null;
	}

	internal (GpuBuffer<CubeVertex> Vertex, GpuBuffer<uint> Index) PrepareMeshBuffers( int faceCount )
	{
		_vertexCount = faceCount * 4;
		_indexCount = faceCount * 6;

		if ( _vertexBuffer is null || _vertexBuffer.ElementCount < _vertexCount )
		{
			_vertexBuffer?.Dispose();
			_vertexBuffer = new GpuBuffer<CubeVertex>( _vertexCount.NextPowerOf2,
				GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		}

		if ( _indexBuffer is null || _indexBuffer.ElementCount < _indexCount )
		{
			_indexBuffer?.Dispose();
			_indexBuffer = new GpuBuffer<uint>( _indexCount.NextPowerOf2,
				GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
		}

		return (_vertexBuffer, _indexBuffer);
	}

	public override void RenderSceneObject()
	{
		if ( _vertexBuffer is null || _indexBuffer is null || _indexCount == 0 ) return;

		Attributes.Set( "WorldOrigin", Position );

		Graphics.Draw(
			vertexBuffer: _vertexBuffer,
			indexBuffer: _indexBuffer,
			material: Material,
			attributes: Attributes,
			indexCount: _indexCount );
	}
}
