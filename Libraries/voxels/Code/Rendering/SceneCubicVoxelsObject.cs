using System.Diagnostics;
using Sandbox;

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

	private GpuBuffer<CompressedVertex>? _vertexBuffer;
	private int _vertexCount;

	public SceneCubicVoxelsObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		Flags.CastShadows = true;
		Flags.IsOpaque = true;
	}

	public void SetVoxels( ReadOnlyVoxelSpan<byte> span, int margin = 1 )
	{
		var timer = Stopwatch.StartNew();

		var innerSize = span.Size - margin * 2;
		var maxFaces = innerSize.x * innerSize.y * innerSize.z * 3;
		var maxVertices = maxFaces * 6;

		if ( _vertexBuffer is null || _vertexBuffer.ElementCount < maxVertices )
		{
			_vertexBuffer = new GpuBuffer<CompressedVertex>( maxVertices, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex | GpuBuffer.UsageFlags.Append );
		}
		
		_vertexBuffer.SetCounterValue( 0 );

		using var voxelBuffer = new GpuBuffer<uint>( span.Source.Length, GpuBuffer.UsageFlags.Structured, "Voxels" );
		using var countBuffer = new GpuBuffer<uint>( 1 );

		voxelBuffer.SetData( span.Source );

		var compute = new ComputeShader( "Shaders/voxels/cubes_cs.shader" );

		compute.Attributes.Set( "FaceBuffer", _vertexBuffer );
		compute.Attributes.Set( "VoxelData", voxelBuffer );
		compute.Attributes.Set( "VoxelOffset", new Vector3Int( margin, margin, margin ) );
		compute.Attributes.Set( "VoxelStride", span.Stride );

		compute.Dispatch( innerSize.x, innerSize.y, innerSize.z );
		
		_vertexBuffer.CopyStructureCount( countBuffer );

		uint[] countData = [0];

		countBuffer.GetData( countData );

		_vertexCount = (int)countData[0] * 6;

		Log.Info( $"Building Mesh: {timer.Elapsed.TotalMilliseconds:F2} ms" );
	}

	public override void RenderSceneObject()
	{
		if ( _vertexBuffer is null ) return;

		Graphics.Draw( _vertexBuffer, Material, attributes: Attributes, vertexCount: _vertexCount );
	}
}
