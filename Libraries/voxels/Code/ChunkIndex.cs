namespace Voxels;

internal readonly record struct ChunkIndex( Vector3Int Position, int Level )
{
	public Vector3Int Min => Position * VoxelChunk.Size * VoxelScale;
	public Vector3Int Max => (Position + 1) * VoxelChunk.Size * VoxelScale;

	public int VoxelScale => 1 << Level;

	public bool Contains( Vector3 pos, int voxelMargin = 0 )
	{
		var thisMin = Min - voxelMargin * VoxelScale;
		var thisMax = Max + voxelMargin * VoxelScale;

		return pos.x >= thisMin.x && pos.x < thisMax.x
			&& pos.y >= thisMin.y && pos.y < thisMax.y
			&& pos.z >= thisMin.z && pos.z < thisMax.z;
	}

	public bool Overlaps( BBox bounds, int voxelMargin = 0 )
	{
		var thisMin = Min - voxelMargin * VoxelScale;
		var thisMax = Max + voxelMargin * VoxelScale;

		return thisMin.x <= bounds.Maxs.x && thisMax.x > bounds.Mins.x
			&& thisMin.y <= bounds.Maxs.y && thisMax.y > bounds.Mins.y
			&& thisMin.z <= bounds.Maxs.z && thisMax.z > bounds.Mins.z;
	}

	public ChunkIndex FirstSubChunk => new ChunkIndex( Position * 2, Level - 1 );

	public override string ToString()
	{
		return $"ChunkIndex {{ {Position}, {Level} }}";
	}
}
