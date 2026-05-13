using Sandbox;
using Sandbox.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Voxels.Rendering;

internal readonly record struct RenderVertex(
	[field: VertexLayout.Position] Vector3 Position,
	[field: VertexLayout.Normal] Vector3 Normal );

public sealed class SceneVoxelsObject : SceneCustomObject
{
	private const int Margin = 1;

	private static Material Material { get; } = Material.FromShader( "Shaders/voxels/cubes.shader" );

	private GpuBuffer<RenderVertex>? _vertexBuffer;
	private GpuBuffer<uint>? _indexBuffer;
	private uint _vertexCount;
	private uint _indexCount;

	public float VoxelScale
	{
		get;
		set
		{
			field = value;
			Attributes.Set( "VoxelScale", value );
		}
	}

	public Vector3Int Size { get; }
	public Vector3Int SizeWithMargin { get; }

	internal int Count { get; }
	internal Vector3Int Offset { get; }
	internal Vector2Int Stride { get; }

	private readonly record struct WorldGenParameters( Vector3Int WorldOffset, int Seed );

	private WorldGenParameters? _worldGenParams;
	private bool _needsWorldGen;

	public bool IsReady => !_needsWorldGen;

	internal GpuBuffer<uint>? VoxelBuffer { get; private set; }

	public SceneVoxelsObject( SceneWorld sceneWorld, Vector3 position, Vector3Int size, float voxelScale ) : base( sceneWorld )
	{
		Size = size;
		SizeWithMargin = size + Margin * 2 + 1;

		Offset = Margin;
		Stride = new Vector2Int( SizeWithMargin.x, SizeWithMargin.x * SizeWithMargin.y );
		Count = SizeWithMargin.x * SizeWithMargin.y * SizeWithMargin.z;

		VoxelScale = voxelScale;

		Flags.CastShadows = true;
		Flags.IsOpaque = true;

		Batchable = false;

		Position = position;

		Attributes.Set( "WorldOrigin", Position );
		Bounds = new BBox( Position, Position + size * voxelScale );
	}

	private static ComputeShader? _generateCompute;

	public bool Generate( Vector3Int worldOffset, int seed )
	{
		var parameters = new WorldGenParameters( worldOffset, seed );

		if ( _worldGenParams == parameters )
		{
			return false;
		}

		_worldGenParams = parameters;
		_needsWorldGen = true;
		return true;
	}

	internal void BeforeUpdate()
	{
		if ( VoxelBuffer is null || VoxelBuffer.ElementCount != Count )
		{
			VoxelBuffer?.Dispose();
			VoxelBuffer = new GpuBuffer<uint>( Count );
		}

		if ( !_needsWorldGen || _worldGenParams is not { } parameters ) return;

		_needsWorldGen = false;

		VoxelBuffer.Clear();

		_generateCompute ??= new ComputeShader( "Shaders/procgen/caveworld.shader" );

		_generateCompute.Attributes.Set( "VoxelData", VoxelBuffer );
		_generateCompute.Attributes.Set( "VoxelCount", SizeWithMargin );
		_generateCompute.Attributes.Set( "VoxelOffset", new Vector3Int( 0, 0, 0 ) );
		_generateCompute.Attributes.Set( "VoxelStride", Stride );
		_generateCompute.Attributes.Set( "VoxelScale", VoxelScale );

		var random = new Random( parameters.Seed );
		var seedOffset = new Vector3Int( random.Next( -32768, 32768 ), random.Next( -32768, 32768 ), 0 );

		_generateCompute.Attributes.Set( "WorldOrigin", parameters.WorldOffset + seedOffset - Offset * VoxelScale );

		_generateCompute.Dispatch( SizeWithMargin.x, SizeWithMargin.y, 1 );
	}

	private static ComputeShader? _editCompute;

	public void Subtract( Capsule capsule )
	{
		_editCompute ??= new ComputeShader( "Shaders/voxels/edit_cs.shader" );

		_editCompute.Attributes.Set( "VoxelData", VoxelBuffer );
		_editCompute.Attributes.Set( "VoxelCount", SizeWithMargin );
		_editCompute.Attributes.Set( "VoxelOffset", new Vector3Int( 0, 0, 0 ) );
		_editCompute.Attributes.Set( "VoxelStride", Stride );
		_editCompute.Attributes.Set( "VoxelScale", VoxelScale );

		_editCompute.Attributes.Set( "EditOriginA", capsule.CenterA );
		_editCompute.Attributes.Set( "EditOriginB", capsule.CenterB );
		_editCompute.Attributes.Set( "EditRadius", capsule.Radius );

		_editCompute.Dispatch( SizeWithMargin.x, SizeWithMargin.y, SizeWithMargin.z );
	}

	internal void ClearMesh()
	{
		_vertexCount = 0;

		_vertexBuffer?.Dispose();
		_vertexBuffer = null;
	}

	internal (GpuBuffer<RenderVertex> Vertices, GpuBuffer<uint> Indices) PrepareRenderBuffers( uint vertexCount, uint indexCount )
	{
		_vertexCount = vertexCount;
		_indexCount = indexCount;

		if ( _vertexBuffer is null || _vertexBuffer.ElementCount < _vertexCount )
		{
			_vertexBuffer?.Dispose();
			_vertexBuffer = new GpuBuffer<RenderVertex>( ((int)_vertexCount).NextPowerOf2,
				GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		}

		if ( _indexBuffer is null || _indexBuffer.ElementCount < _indexCount )
		{
			_indexBuffer?.Dispose();
			_indexBuffer = new GpuBuffer<uint>( ((int)_indexCount).NextPowerOf2,
				GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
		}

		return (_vertexBuffer, _indexBuffer);
	}

	public override void RenderSceneObject()
	{
		if ( _vertexBuffer is null || _indexBuffer is null || _indexCount == 0 ) return;

		Graphics.Draw(
			vertexBuffer: _vertexBuffer,
			indexBuffer: _indexBuffer,
			material: Material,
			attributes: Attributes,
			indexCount: (int)_indexCount );
	}
}
