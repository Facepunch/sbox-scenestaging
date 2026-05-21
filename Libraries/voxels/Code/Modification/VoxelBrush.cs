using System;
using System.Collections.Generic;
using Sandbox;

namespace Voxels.Modification;

internal readonly record struct VoxelModification( uint ModificationTypeId, uint ParameterOffset );

public enum BrushOperation
{
	Add,
	Subtract
}

public abstract class VoxelBrush : Component, Component.ExecuteInEditor
{
	public virtual BBox LocalBounds => new( mins: float.NegativeInfinity, maxs: float.PositiveInfinity );
	public BBox WorldBounds => LocalBounds + WorldPosition;
	public abstract uint ModificationTypeId { get; }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Transform.OnTransformChanged += OnTransformChanged;
		GetComponentInParent<VoxelVolume>()?.ForceRebuild();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Transform.OnTransformChanged -= OnTransformChanged;
		GetComponentInParent<VoxelVolume>()?.ForceRebuild();
	}

	private void OnTransformChanged()
	{
		GetComponentInParent<VoxelVolume>()?.ForceRebuild();
	}

	internal bool CanAffectChunk( VoxelChunk chunk )
	{
		var chunkMin = chunk.Index.Min * chunk.Volume.VoxelSize;
		var chunkMax = chunk.Index.Max * chunk.Volume.VoxelSize;
		var chunkBounds = new BBox( chunkMin, chunkMax );

		// Expand bounds by this many voxels when checking for overlap

		const float voxelMargin = VoxelChunk.Margin + 2f;

		return WorldBounds.Overlaps( chunkBounds.Grow( chunk.VoxelScale * voxelMargin ) );
	}

	internal bool Write( VoxelChunk chunk, List<VoxelModification> modifications, List<uint> parameters )
	{
		var parameterOffset = (uint)parameters.Count;

		modifications.Add( new VoxelModification( ModificationTypeId, parameterOffset ) );

		var writer = new ParameterWriter( parameters );

		OnWriteParameters( writer );

		return true;
	}

	protected readonly struct ParameterWriter
	{
		private readonly List<uint> _list;

		internal ParameterWriter( List<uint> list )
		{
			_list = list;
		}

		public void Write( uint value ) => _list.Add( value );
		public void Write( int value ) => _list.Add( unchecked( (uint)value ) );
		public void Write( float value ) => _list.Add( BitConverter.SingleToUInt32Bits( value ) );

		public void Write( Vector2 value )
		{
			Write( value.x );
			Write( value.y );
		}

		public void Write( Vector3 value )
		{
			Write( value.x );
			Write( value.y );
			Write( value.z );
		}
	}

	protected virtual void OnWriteParameters( ParameterWriter writer ) { }
}
