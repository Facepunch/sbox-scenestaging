namespace Voxels;

internal readonly record struct ChunkIndex( Vector3Int Position, int Level )
{
	public Vector3Int Min => Position * VoxelChunk.Size * VoxelScale;
	public Vector3Int Max => (Position + 1) * VoxelChunk.Size * VoxelScale;

	public int VoxelScale => 1 << Level;

	public bool Contains( Vector3Int pos )
	{
		var thisMin = Min;
		var thisMax = Max;

		return pos.x >= thisMin.x && pos.x < thisMax.x
		                          && pos.y >= thisMin.y && pos.y < thisMax.y
		                          && pos.z >= thisMin.z && pos.z < thisMax.z;
	}

	public bool Contains( Vector3Int min, Vector3Int max )
	{
		var thisMin = Min;
		var thisMax = Max;

		return thisMin.x <= max.x && thisMax.x > min.x
		                          && thisMin.y <= max.y && thisMax.y > min.y
		                          && thisMin.z <= max.z && thisMax.z > min.z;
	}

	public ChunkIndex FirstSubChunk => new ChunkIndex( Position * 2, Level - 1 );

	public override string ToString()
	{
		return $"ChunkIndex {{ {Position}, {Level} }}";
	}
}
