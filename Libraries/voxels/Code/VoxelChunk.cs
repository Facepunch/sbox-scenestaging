using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
	public bool HasPendingModifications => IsValid && (_wasCleared || _appliedModifications < _modifications.Count);

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
	}

	private readonly List<VoxelModification> _modifications = new();
	private int _appliedModifications;
	private bool _wasCleared = true;

	private void ClearVoxelBuffer()
	{
		_buffer.Clear( Volume.StartSolid ? 255U : 0 );
	}

	public void Clear()
	{
		_wasCleared = true;
		_appliedModifications = 0;
		_modifications.Clear();

		Volume.Scene.Get<VoxelSystem>().QueueChunkUpdate( this );
	}

	public void SetModifications( IReadOnlyList<VoxelModification> modifications )
	{
		var canAppend = modifications.Count >= _modifications.Count;

		for ( var i = 0; i < _modifications.Count && canAppend; i++ )
		{
			if ( !_modifications[i].Equals( modifications[i] ) )
			{
				canAppend = false;
			}
		}

		if ( canAppend )
		{
			AppendModifications( modifications.Skip( _modifications.Count ) );
			return;
		}

		if ( modifications.Count == 0 )
		{
			if ( _modifications.Count > 0 )
			{
				Clear();
			}

			return;
		}

		_wasCleared = true;
		_appliedModifications = 0;
		_modifications.Clear();

		AppendModifications( modifications );
	}

	public void AppendModifications( IEnumerable<VoxelModification> modifications )
	{
		var prevCount = _modifications.Count;

		_modifications.AddRange( modifications );

		if ( _modifications.Count == prevCount ) return;

		Volume.Scene.Get<VoxelSystem>().QueueChunkUpdate( this );
	}

	public void AppendModification( VoxelModification modification )
	{
		_modifications.Add( modification );

		Volume.Scene.Get<VoxelSystem>().QueueChunkUpdate( this );
	}

	internal void WritePendingModifications( List<VoxelModificationEntry> modifications, List<uint> parameters )
	{
		if ( _wasCleared )
		{
			_wasCleared = false;
			ClearVoxelBuffer();
		}

		for ( ; _appliedModifications < _modifications.Count; _appliedModifications++ )
		{
			var modification = _modifications[_appliedModifications];

			modification.Write( modifications, parameters );
		}
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
