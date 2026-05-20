using Sandbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Voxels.Rendering;

internal readonly record struct RenderVertex(
	[field: VertexLayout.Position] Vector3 Position,
	[field: VertexLayout.Normal] Vector3 Normal );

public sealed class SceneVoxelsObject : SceneCustomObject
{
	private const int Margin = 1;

	private static Material Material { get; } = Material.FromShader( "Shaders/voxels/cubes.shader" );

	private GpuBuffer<RenderVertex>? _vertexBuffer;
	private GpuBuffer<Vector3>? _physicsVertexBuffer;
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

	internal uint Generation { get; private set; }
	internal int Count { get; }
	internal Vector3Int Offset { get; }
	internal Vector2Int Stride { get; }

	internal PhysicsBody? Body { get; set; }
	internal PhysicsShape? Shape { get; set; }

	private readonly record struct WorldGenParameters( Vector3Int WorldOffset, int Seed );

	private WorldGenParameters? _worldGenParams;
	private bool _needsWorldGen;
	private bool _finishedFirstMeshUpdate;
	private bool _finishedFirstPhysicsUpdate;

	public bool IsMeshReady => _finishedFirstMeshUpdate;
	public bool IsPhysicsReady => _finishedFirstPhysicsUpdate;

	internal GpuBuffer<uint>? VoxelBuffer { get; private set; }

	public SceneVoxelsObject( SceneWorld sceneWorld, Vector3Int size ) : base( sceneWorld )
	{
		Size = size;
		SizeWithMargin = size + Margin * 2 + 1;

		Offset = Margin;
		Stride = new Vector2Int( SizeWithMargin.x, SizeWithMargin.x * SizeWithMargin.y );
		Count = SizeWithMargin.x * SizeWithMargin.y * SizeWithMargin.z;

		Flags.CastShadows = true;
		Flags.IsOpaque = true;

		Batchable = false;
	}

	public void Initialize( Vector3 position, float voxelScale )
	{
		Generation++;

		VoxelScale = voxelScale;
		Position = position;

		var hue = (MathF.Log2( voxelScale ) - 5f) * 60f;
		var color = new ColorHsv( hue, 0.75f, 1f ).ToColor();

		Attributes.Set( "WorldOrigin", Position );
		Attributes.Set( "WorldSize", Size * voxelScale );
		Attributes.Set( "Tint", new Vector3( color.r, color.g, color.b ) );

		Bounds = new BBox( Position, Position + Size * voxelScale );

		_worldGenParams = null;
		_needsWorldGen = false;

		_vertexCount = 0;
		_indexCount = 0;

		RenderingEnabled = true;

		_finishedFirstMeshUpdate = false;
		_finishedFirstPhysicsUpdate = false;
		_shouldRequestPhysicsMesh = false;
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

	public void ClearMesh()
	{
		_vertexCount = 0;
		_indexCount = 0;
		_finishedFirstMeshUpdate = true;
		_finishedFirstPhysicsUpdate = true;
		_shouldRequestPhysicsMesh = false;

		_physicsVertices.Clear();
		_physicsIndices.Clear();

		Shape?.Remove();
		Shape = null;
	}

	public void Reset()
	{
		ClearMesh();
		Body?.Remove();
		Body = null;
		RenderingEnabled = false;
		LodMask = 0;
		Generation++;
	}

	private bool _shouldRequestPhysicsMesh;
	private bool _hasPhysicsVertices;
	private bool _hasPhysicsIndices;

	internal (GpuBuffer<RenderVertex> Vertices, GpuBuffer<Vector3>? PhysicsVertices, GpuBuffer<uint> Indices) PrepareRenderBuffers( uint vertexCount, uint indexCount )
	{
		_vertexCount = vertexCount;
		_indexCount = indexCount;

		_finishedFirstMeshUpdate = true;

		if ( _vertexBuffer is null || _vertexBuffer.ElementCount < _vertexCount )
		{
			_vertexBuffer?.Dispose();
			_vertexBuffer = new GpuBuffer<RenderVertex>( ((int)_vertexCount).NextPowerOf2,
				GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
		}

		if ( Body is not null )
		{
			if ( _physicsVertexBuffer is null || _physicsVertexBuffer.ElementCount < _vertexCount )
			{
				_physicsVertexBuffer?.Dispose();
				_physicsVertexBuffer = new GpuBuffer<Vector3>( ((int)_vertexCount).NextPowerOf2 );
			}

			_shouldRequestPhysicsMesh = true;
		}

		if ( _indexBuffer is null || _indexBuffer.ElementCount < _indexCount )
		{
			_indexBuffer?.Dispose();
			_indexBuffer = new GpuBuffer<uint>( ((int)_indexCount).NextPowerOf2,
				GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Index );
		}

		return (_vertexBuffer, Body is not null ? _physicsVertexBuffer : null, _indexBuffer);
	}

	private readonly List<Vector3> _physicsVertices = new();
	private readonly List<int> _physicsIndices = new();

	internal async Task SetPhysicsMeshAsync( uint generation )
	{
		if ( !Body.IsValid() ) return;

		await MainThread.Wait();

		if ( generation != Generation ) return;

		if ( !Shape.IsValid() || !Shape.IsMeshShape )
		{
			Shape?.Remove();
			Shape = Body.AddMeshShape( _physicsVertices, _physicsIndices );
		}
		else
		{
			Shape.UpdateMesh( _physicsVertices, _physicsIndices );
		}

		_finishedFirstPhysicsUpdate = true;
	}

	internal byte LodMask { get; set; }
	internal float LodDistance { get; set; }

	public override void RenderSceneObject()
	{
		if ( _vertexBuffer is null || _indexBuffer is null || _indexCount == 0 ) return;

		if ( _shouldRequestPhysicsMesh && _indexCount > 0 )
		{
			_shouldRequestPhysicsMesh = false;
			_hasPhysicsVertices = false;
			_hasPhysicsIndices = false;

			_physicsVertices.Clear();
			_physicsIndices.Clear();

			var requestGen = Generation;

			_physicsVertexBuffer?.GetDataAsync( vertices =>
			{
				if ( requestGen != Generation ) return;

				_physicsVertices.AddRange( vertices );
				_hasPhysicsVertices = true;

				if ( _hasPhysicsIndices )
				{
					_ = SetPhysicsMeshAsync( requestGen );
				}
			}, 0, (int)_vertexCount );

			_indexBuffer?.GetDataAsync<int>( indices =>
			{
				if ( requestGen != Generation ) return;

				_physicsIndices.AddRange( indices );
				_hasPhysicsIndices = true;

				if ( _hasPhysicsVertices )
				{
					_ = SetPhysicsMeshAsync( requestGen );
				}
			}, 0, (int)_indexCount );
		}

		if ( LodMask == 0xff ) return;

		Attributes.Set( "LodDistance", LodDistance );
		Attributes.Set( "LodMask", (uint)LodMask );

		Graphics.Draw(
			vertexBuffer: _vertexBuffer,
			indexBuffer: _indexBuffer,
			material: Material,
			attributes: Attributes,
			indexCount: (int)_indexCount );
	}
}
