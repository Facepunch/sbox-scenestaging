using Sandbox;
using Sandbox.UI;
using System;
using System.Diagnostics;

namespace Voxels.Rendering;

public sealed class SceneCubicVoxelsObject : SceneCustomObject
{
	private static Material Material { get; } = Material.FromShader( "Shaders/voxels/cubes.shader" );

	private enum CubeFace
	{
		NegX,
		PosX,

		NegY,
		PosY,

		NegZ,
		PosZ
	}

	private readonly record struct CompressedVertex( [field: VertexLayout.TexCoord] uint Packed )
	{
		private static uint Pack( Vector3Int position, CubeFace face, Vector2Int texCoord )
		{
			return ((uint)position.x & 0xff) | (((uint)position.y & 0xff) << 8) | (((uint)position.z & 0xff) << 16)
				| (((uint)face & 0x7) << 24) | (((uint)texCoord.x & 0x1) << 27) | (((uint)texCoord.y & 0x1) << 28);
		}

		public CompressedVertex( Vector3Int position, CubeFace face, Vector2Int texCoord )
			: this( Pack( position, face, texCoord ) )
		{
		}
	}

	private GpuBuffer<uint>? _voxelBuffer;
	private GpuBuffer<CompressedVertex>? _vertexBuffer;
	private int _vertexCount;

	public SceneCubicVoxelsObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		Flags.CastShadows = true;
		Flags.IsOpaque = true;
	}

	public void Generate( Vector3Int size, int seed )
	{
		var timer = Stopwatch.StartNew();

		var sizeWithMargin = size + 2;
		var voxelCount = sizeWithMargin.x * sizeWithMargin.y * sizeWithMargin.z;

		if ( _voxelBuffer is null || _voxelBuffer.ElementCount != voxelCount )
		{
			_voxelBuffer?.Dispose();
			_voxelBuffer = new GpuBuffer<uint>( voxelCount );
		}

		var procGenCompute = new ComputeShader( "Shaders/procgen/caveworld.shader" );

		procGenCompute.Attributes.Set( "VoxelData", _voxelBuffer );
		procGenCompute.Attributes.Set( "VoxelOffset", new Vector3Int( 1, 1, 1 ) );
		procGenCompute.Attributes.Set( "VoxelStride", new Vector2Int( sizeWithMargin.x, sizeWithMargin.x * sizeWithMargin.y ) );

		var random = new Random( seed );

		procGenCompute.Attributes.Set( "WorldOrigin", new Vector3( random.VectorInSquare( 16384f ), 0f ) );

		procGenCompute.Dispatch( size.x, size.y, size.z );

		var maxFaces = size.x * size.y * size.z * 3;
		var maxVertices = maxFaces * 6;

		if ( _vertexBuffer is null || _vertexBuffer.ElementCount < maxVertices )
		{
			_vertexBuffer = new GpuBuffer<CompressedVertex>( maxVertices, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex | GpuBuffer.UsageFlags.Append );
		}

		_vertexBuffer.SetCounterValue( 0 );

		var buildMeshCompute = new ComputeShader( "Shaders/voxels/cubes_cs.shader" );

		buildMeshCompute.Attributes.Set( "FaceBuffer", _vertexBuffer );
		buildMeshCompute.Attributes.Set( "VoxelData", _voxelBuffer );
		buildMeshCompute.Attributes.Set( "VoxelOffset", new Vector3Int( 1, 1, 1 ) );
		buildMeshCompute.Attributes.Set( "VoxelStride", new Vector2Int( sizeWithMargin.x, sizeWithMargin.x * sizeWithMargin.y ) );

		buildMeshCompute.Dispatch( size.x, size.y, size.z );

		using var countBuffer = new GpuBuffer<uint>( 1 );

		_vertexBuffer.CopyStructureCount( countBuffer );

		uint[] countData = [0];

		countBuffer.GetData( countData );

		_vertexCount = (int)countData[0] * 6;

		Log.Info( $"Generated in {timer.Elapsed.TotalMilliseconds:F2}ms" );
	}

	public override void RenderSceneObject()
	{
		if ( _vertexBuffer is null || _vertexCount == 0 ) return;

		Graphics.Draw( _vertexBuffer, Material, attributes: Attributes, vertexCount: _vertexCount );
	}
}
