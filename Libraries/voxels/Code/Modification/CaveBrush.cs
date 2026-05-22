using System;
using System.Numerics;
using Sandbox;

namespace Voxels.Modification;

public sealed record CaveModification( Vector3 WorldOrigin, Vector3 WorldSize, Rotation WorldRotation, Vector3 NoiseOffset )
	: VoxelModification( 0x05, GetBounds( WorldOrigin, WorldSize, WorldRotation ) )
{
	private static BBox GetBounds( Vector3 worldOrigin, Vector3 worldSize, Rotation worldRotation )
	{
		var extents = worldSize.Length * 0.5f;

		return new BBox( worldOrigin - extents, worldOrigin + extents );
	}

	protected override void OnWriteParameters( ParameterWriter writer )
	{
		writer.Write( WorldOrigin );
		writer.Write( WorldSize );
		writer.Write( WorldRotation );
		writer.Write( NoiseOffset );
	}
}

public sealed class CaveBrush : VoxelBrush
{
	[Property]
	public Vector3 Size
	{ 
		get;
		set
		{
			if ( value.Equals( field ) ) return;

			field = value;
			UpdateModification();
		}
	} = new Vector3( 512f, 512f, 256f );

	[Property]
	public int Seed
	{
		get;
		set
		{
			if ( value == field ) return;

			field = value;
			UpdateModification();
		}
	}

	[Button]
	public void RandomizeSeed()
	{
		Seed = Random.Shared.Next();
	}

	protected override VoxelModification BuildModification()
	{
		var random = new Random( Seed );

		return new CaveModification( WorldPosition, Size, WorldRotation,
			new Vector3(
				random.Float( -8192f, 8192f ),
				random.Float( -8192f, 8192f ),
				random.Float( -8192f, 8192f ) ) );
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected ) return;

		Gizmo.Transform = Gizmo.Transform.ToWorld( new Transform( position: 0f, rotation: Rotation.Identity, scale: Size ) );
		Gizmo.Draw.LineSphere( new Sphere( 0f, 0.5f ) );
	}
}
