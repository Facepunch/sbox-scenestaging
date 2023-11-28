namespace Sandbox.TerrainEngine;

/// <summary>
/// The TerrainData class stores heightmaps, splatmaps, etc.
/// The <see cref="Terrain"/> component links to the terrain data and renders it.
/// </summary>
/// <remarks>
/// I am torn between having this be an asset or not.
/// </remarks>
// [GameResource( "Terrain Data", "terrain", "Full of grass" )]
public class TerrainData // : GameResource
{
	public ushort[] HeightMap { get; set; }

	public int HeightMapWidth { get; set; }
	public int HeightMapHeight { get; set; }

	public float MaxHeight { get; set; }

	public TerrainData()
	{
		HeightMapWidth = 513;
		HeightMapHeight = 513;
		HeightMap = new ushort[HeightMapWidth * HeightMapHeight];
		MaxHeight = 1024.0f;
	}

	public void SetSize( int width, int height )
	{
		HeightMap = new ushort[width * height];
	}

	public void SetHeight( int x, int y, ushort value )
	{
		var index = y * HeightMapWidth + x;
		if ( index < 0 || index >= HeightMap.Length )
		{
			return;
		}

		HeightMap[y * HeightMapWidth + x] = value;
	}

	public bool InRange( int x, int y )
	{
		if ( x < 0 || x >= HeightMapWidth ) return false;
		if ( y < 0 || y >= HeightMapHeight ) return false;
		return true;
	}

	public ushort GetHeight( int x, int y )
	{
		var index = y * HeightMapWidth + x;
		if ( index < 0 || index >= HeightMap.Length )
		{
			return 0;
		}

		return HeightMap[y * HeightMapWidth + x];
	}
}
