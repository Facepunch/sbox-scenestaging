using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.Services;
using System;
using System.Collections.Generic;
using Voxels.Modification;
using Voxels.Physics;
using Voxels.Rendering;

namespace Voxels;

internal readonly record struct GpuVoxelSpan(
	GpuBuffer<uint> Buffer,
	Vector3Int Offset,
	Vector3Int Size,
	Vector3Int Stride );

internal sealed class VoxelChunk : IDisposable, IValid
{
	private static GpuBufferPool<uint> VoxelBufferPool { get; } = new();

	public const int Size = 32;
	public const int Margin = 1;

	public VoxelVolume Volume { get; }
	public ChunkIndex Index { get; }
	public float VoxelScale { get; }

	public bool IsValid { get; private set; }

	private readonly GpuBuffer<uint> _buffer;

	private SceneVoxelsObject? _sceneObject;
	private PhysicsBody? _physicsBody;
	private PhysicsShape? _physicsShape;

	internal byte LodMask { get; set; }
	internal float LodDistance { get; set; }

	internal GpuVoxelSpan FullVoxelSpan { get; }

	internal GpuVoxelSpan VisibleVoxelSpan { get; }

	internal VoxelRenderMesh? RenderMesh
	{
		get;
		set
		{
			Assert.IsValid( this );

			field = value;
			IsReady = true;

			if ( value is null )
			{
				_sceneObject?.Delete();
				_sceneObject = null;
			}
			else if ( _sceneObject is null )
			{
				_sceneObject = new SceneVoxelsObject( this );
				_sceneObject.Initialize( Index.Min * Volume.VoxelSize, Size, VoxelScale );
			}
		}
	}

	internal VoxelCollisionMesh? CollisionMesh
	{
		get;
		set
		{
			Assert.IsValid( this );

			field = value;
			IsPhysicsReady = true;

			if ( value is null )
			{
				_physicsBody?.Remove();
				_physicsBody = null;
				_physicsShape = null;
			}
			else
			{
				_physicsBody ??= new PhysicsBody( Volume.Scene.PhysicsWorld )
				{
					Component = Volume,
					Position = Index.Min * Volume.VoxelSize
				};

				value.UpdateShape( _physicsBody, ref _physicsShape );
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

		_buffer = VoxelBufferPool.Rent( stride.z );

		FullVoxelSpan = new GpuVoxelSpan( _buffer, 0, sizeWithMargin, stride );
		VisibleVoxelSpan = new GpuVoxelSpan( _buffer, 1, Size, stride );

		IsValid = true;

		ClearVoxelBuffer();
	}

	private readonly List<VoxelBrush> _brushes = new();
	private int _appliedBrushes;

	private void ClearVoxelBuffer()
	{
		_buffer.Clear( Volume.StartSolid ? 255U : 0 );
	}

	public void Clear()
	{
		_appliedBrushes = 0;

		_brushes.Clear();
		ClearVoxelBuffer();

		Volume.Scene.Get<VoxelSystem>().QueueChunkUpdate( this );
	}

	public void SetBrushes( IEnumerable<VoxelBrush> brushes )
	{
		var hadBrushes = _brushes.Count > 0;

		if ( hadBrushes )
		{
			_appliedBrushes = 0;
			_brushes.Clear();

			ClearVoxelBuffer();
		}

		AppendBrushes( brushes );

		if ( _brushes.Count == 0 && hadBrushes )
		{
			RenderMesh = null;
			CollisionMesh = null;
		}
	}

	public void AppendBrushes( IEnumerable<VoxelBrush> brushes )
	{
		foreach ( var brush in brushes )
		{
			if ( !brush.CanAffectChunk( this ) ) continue;

			_brushes.Add( brush );
		}

		ApplyBrushes();
	}

	public void AppendBrush( VoxelBrush brush )
	{
		if ( !brush.CanAffectChunk( this ) ) return;

		_brushes.Add( brush );

		ApplyBrushes();
	}

	private static ComputeShader? _modificationCompute;
	private static GpuBuffer<VoxelModification>? _modificationBuffer;
	private static GpuBuffer<uint>? _parameterBuffer;

	private readonly List<VoxelModification> _modificationList = new();
	private readonly List<uint> _parameterList = new();

	public void ApplyBrushes()
	{
		if ( _appliedBrushes >= _brushes.Count ) return;

		_modificationList.Clear();
		_parameterList.Clear();

		for ( ; _appliedBrushes < _brushes.Count; _appliedBrushes++ )
		{
			var brush = _brushes[_appliedBrushes];

			if ( !brush.IsValid ) continue;

			brush.Write( this, _modificationList, _parameterList );
		}

		if ( _modificationList.Count == 0 ) return;

		if ( _modificationBuffer is null || _modificationBuffer.ElementCount < _modificationList.Count )
		{
			_modificationBuffer?.Dispose();
			_modificationBuffer = new GpuBuffer<VoxelModification>( _modificationList.Count.NextPowerOf2 );
		}

		if ( _parameterBuffer is null || _parameterBuffer.ElementCount < _parameterList.Count )
		{
			_parameterBuffer?.Dispose();
			_parameterBuffer = new GpuBuffer<uint>( _parameterList.Count.NextPowerOf2 );
		}

		_modificationCompute ??= new ComputeShader( "Shaders/voxels/modification_cs.shader" );

		_modificationCompute.Attributes.Set( "VoxelData", _buffer );
		_modificationCompute.Attributes.Set( "VoxelCount", FullVoxelSpan.Size );
		_modificationCompute.Attributes.Set( "VoxelOffset", FullVoxelSpan.Offset );
		_modificationCompute.Attributes.Set( "VoxelStride", FullVoxelSpan.Stride );

		_modificationCompute.Attributes.Set( "VoxelScale", VoxelScale );
		_modificationCompute.Attributes.Set( "WorldOrigin", Index.Min * Volume.VoxelSize - Margin * VoxelScale );

		_modificationBuffer.SetData( _modificationList );
		_parameterBuffer.SetData( _parameterList );

		_modificationCompute.Attributes.Set( "ModificationCount", _modificationList.Count );
		_modificationCompute.Attributes.Set( "ModificationList", _modificationBuffer );
		_modificationCompute.Attributes.Set( "ParameterData", _parameterBuffer );

		_modificationCompute.Dispatch( FullVoxelSpan.Size.x, FullVoxelSpan.Size.y, 1 );

		Volume.Scene.Get<VoxelSystem>().QueueChunkUpdate( this );
	}

	public void Dispose()
	{
		IsValid = false;

		VoxelBufferPool.Return( _buffer );

		RenderMesh?.Dispose();

		_sceneObject?.Delete();
		_sceneObject = null;

		_physicsBody?.Remove();
		_physicsBody = null;
		_physicsShape = null;
	}
}
