using Sandbox;
using System;

namespace Voxels.Rendering;

internal readonly record struct RenderVertex(
	[field: VertexLayout.Position] Vector3 Position,
	[field: VertexLayout.Normal] Vector3 Normal );

internal sealed class SceneVoxelsObject : SceneCustomObject
{
	private static Material Material { get; } = Material.FromShader( "Shaders/voxels/cubes.shader" );

	public VoxelChunk Chunk { get; }

	public SceneVoxelsObject( VoxelChunk chunk )
		: base( chunk.Volume.Scene.SceneWorld )
	{
		Chunk = chunk;

		Flags.CastShadows = true;
		Flags.IsOpaque = true;

		Batchable = false;
	}

	public void Initialize( Vector3 position, Vector3Int size, float voxelScale )
	{
		Position = position;

		var hue = (MathF.Log2( voxelScale ) - 5f) * 60f;
		var color = new ColorHsv( hue, 0.75f, 1f ).ToColor();

		Attributes.Set( "VoxelScale", voxelScale );
		Attributes.Set( "WorldOrigin", Position );
		Attributes.Set( "WorldSize", size * voxelScale );
		Attributes.Set( "Tint", new Vector3( color.r, color.g, color.b ) );

		Bounds = new BBox( Position, Position + size * voxelScale );

		RenderingEnabled = true;
	}

	public override void RenderSceneObject()
	{
		if ( Chunk.LodMask == 0xff ) return;
		if ( Chunk.RenderMesh is not { } mesh ) return;

		Attributes.Set( "LodDistance", Chunk.LodDistance );
		Attributes.Set( "LodMask", (uint)Chunk.LodMask );

		mesh.Draw( Material, Attributes );
	}
}
