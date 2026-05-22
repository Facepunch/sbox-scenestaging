using System;
using Sandbox;

namespace Voxels.Modification;

public sealed record MountainModification( Vector3 WorldOrigin, float WorldRadius, float WorldHeight, float NoiseOffset )
	: VoxelModification( 0x04, new BBox( WorldOrigin - new Vector3( WorldRadius, WorldRadius, 0f ), WorldOrigin + new Vector3( WorldRadius, WorldRadius, WorldHeight ) ) )
{
	protected override void OnWriteParameters( ParameterWriter writer )
	{
		writer.Write( WorldOrigin );
		writer.Write( WorldRadius );
		writer.Write( WorldHeight );
		writer.Write( NoiseOffset );
	}
}

public sealed class MountainBrush : VoxelBrush
{
	[Property]
	public float Height
	{
		get;
		set
		{
			if ( value.Equals( field ) ) return;

			field = value;
			UpdateModification();
		}
	} = 4096f;

	[Property]
	public float Radius
	{
		get;
		set
		{
			if ( value.Equals( field ) ) return;

			field = value;
			UpdateModification();
		}
	} = 8192f;

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
		var xyScale = (WorldScale.x + WorldScale.y) * 0.5f;
		var random = new Random( Seed );

		return new MountainModification( WorldPosition, Radius * xyScale, Height * WorldScale.z, random.NextSingle() * 8192f - 4096f );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.LineCircle( 0f, Vector3.Up, Radius );
		Gizmo.Draw.Arrow( 0f, Vector3.Up * Height );
	}
}
