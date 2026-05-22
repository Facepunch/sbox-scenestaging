using Sandbox;

namespace Voxels.Modification;

public sealed record WorldGenModification()
	: VoxelModification( 0x03 );

public sealed class WorldGenBrush : VoxelBrush
{
	protected override VoxelModification BuildModification()
	{
		return new WorldGenModification();
	}
}
