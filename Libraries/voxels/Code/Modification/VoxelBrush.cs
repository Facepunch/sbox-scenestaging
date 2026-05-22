using System;
using System.Collections.Generic;
using Sandbox;

namespace Voxels.Modification;

internal readonly record struct VoxelModificationEntry( uint ModificationTypeId, uint ParameterOffset );

public abstract record VoxelModification( uint ModificationTypeId, BBox WorldBounds )
{
	protected VoxelModification( uint modificationTypeId )
		: this( modificationTypeId, new BBox( mins: float.NegativeInfinity, maxs: float.PositiveInfinity ) )
	{

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

	internal bool Write( List<VoxelModificationEntry> modifications, List<uint> parameters )
	{
		var parameterOffset = (uint)parameters.Count;

		modifications.Add( new VoxelModificationEntry( ModificationTypeId, parameterOffset ) );

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
		public void Write( int value ) => _list.Add( unchecked((uint)value) );
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

		public void Write( Rotation value )
		{
			Write( value.x );
			Write( value.y );
			Write( value.z );
			Write( value.w );
		}
	}

	protected virtual void OnWriteParameters( ParameterWriter writer ) { }
}

public enum BrushOperation
{
	Add,
	Subtract
}

public abstract class VoxelBrush : Component, Component.ExecuteInEditor
{
	public VoxelModification? Modification { get; private set; }

	protected abstract VoxelModification BuildModification();

	protected VoxelVolume? Volume => GetComponentInParent<VoxelVolume>();

	protected override void OnEnabled()
	{
		base.OnEnabled();

		Transform.OnTransformChanged += OnTransformChanged;

		UpdateModification();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		Transform.OnTransformChanged -= OnTransformChanged;

		UpdateModification();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		Transform.OnTransformChanged -= OnTransformChanged;

		UpdateModification();
	}

	private void OnTransformChanged()
	{
		UpdateModification();
	}

	protected void UpdateModification()
	{
		var prevModification = Modification;
		var nextModification = Active ? BuildModification() : null;

		if ( prevModification?.Equals( nextModification ) is true )
		{
			return;
		}

		Modification = nextModification;

		OnModificationChanged( prevModification, nextModification );
	}

	private void OnModificationChanged( VoxelModification? prev, VoxelModification? next )
	{
		if ( GetComponentInParent<VoxelVolume>() is not { } volume ) return;

		if ( prev is not null )
		{
			volume.MarkDirty( prev.WorldBounds );
		}

		if ( next is not null )
		{
			volume.MarkDirty( next.WorldBounds );
		}
	}
}
