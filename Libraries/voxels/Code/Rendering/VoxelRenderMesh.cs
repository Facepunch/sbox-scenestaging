using Sandbox;
using System;
using Sandbox.Diagnostics;

namespace Voxels.Rendering;

internal sealed class VoxelRenderMesh : IDisposable, IValid
{
	private static GpuBufferPool<RenderVertex> VertexBufferPool { get; } = new( GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
	private static GpuBufferPool<uint> IndexBufferPool { get; } = new( GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );

	private readonly GpuBuffer<RenderVertex> _vertices;
	private readonly GpuBuffer<uint> _indices;

	public int VertexCount { get; }
	public int IndexCount { get; }
	public bool IsValid { get; private set; }

	public VoxelRenderMesh(
		GpuBuffer<RenderVertex> vertices, int vertexOffset, int vertexCount,
		GpuBuffer<uint> indices, int indexOffset, int indexCount )
	{
		_vertices = VertexBufferPool.Rent( vertexCount );
		_indices = IndexBufferPool.Rent( indexCount );

		VertexCount = vertexCount;
		IndexCount = indexCount;

		vertices.CopyTo( _vertices, vertexOffset, 0, vertexCount );
		indices.CopyTo( _indices, indexOffset, 0, indexCount );

		IsValid = true;
	}

	public void Draw( Material material, RenderAttributes attributes )
	{
		Assert.IsValid( this );

		if ( _vertices is not { } vertices ) return;
		if ( _indices is not { } indices ) return;
		if ( IndexCount <= 0 ) return;

		Graphics.Draw(
			vertexBuffer: vertices,
			indexBuffer: indices,
			material: material,
			attributes: attributes,
			indexCount: IndexCount );
	}

	public void Dispose()
	{
		IsValid = false;

		VertexBufferPool.Return( _vertices );
		IndexBufferPool.Return( _indices );
	}
}
