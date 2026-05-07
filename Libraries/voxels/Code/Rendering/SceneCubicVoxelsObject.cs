using System.Collections.Generic;
using Sandbox;
using Sandbox.Rendering;

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

	private readonly GpuBuffer<CompressedVertex> _vertexBuffer;

	public SceneCubicVoxelsObject( SceneWorld sceneWorld ) : base( sceneWorld )
	{
		_vertexBuffer = new GpuBuffer<CompressedVertex>( 6, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		_vertexBuffer.SetData( new List<CompressedVertex>
		{
			new( new Vector3Int( 0, 0, 0 ), CubeFace.PosX, new Vector2Int( 0, 0 ) ),
			new( new Vector3Int( 0, 1, 0 ), CubeFace.PosX, new Vector2Int( 1, 0 ) ),
			new( new Vector3Int( 0, 0, 1 ), CubeFace.PosX, new Vector2Int( 0, 1 ) ),
			new( new Vector3Int( 0, 1, 0 ), CubeFace.PosX, new Vector2Int( 1, 0 ) ),
			new( new Vector3Int( 0, 1, 1 ), CubeFace.PosX, new Vector2Int( 1, 1 ) ),
			new( new Vector3Int( 0, 0, 1 ), CubeFace.PosX, new Vector2Int( 0, 1 ) )
		} );

		RenderCommandList = new CommandList( "CubeVoxels" );
		RenderCommandList.Draw( _vertexBuffer, Material, attributes: Attributes );
	}

	public void SetVoxels( ReadOnlyVoxelSpan<byte> span )
	{
		
	}
}
