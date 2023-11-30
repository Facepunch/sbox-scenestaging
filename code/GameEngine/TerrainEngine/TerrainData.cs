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

	/// <summary>
	/// Pass texel units, returns a normalized float.
	/// </summary>
	public float GetInterpolatedHeight( float x, float y )
	{
		int xFloor = (int)MathF.Floor( x );
		int xCeil = (int)MathF.Ceiling( x );
		int yFloor = (int)MathF.Floor( y );
		int yCeil = (int)MathF.Ceiling( y );

		float q11 = (float)GetHeight( xFloor, yFloor ) / (float)ushort.MaxValue;
		float q12 = (float)GetHeight( xFloor, yCeil ) / (float)ushort.MaxValue;
		float q21 = (float)GetHeight( xCeil, yFloor ) / (float)ushort.MaxValue;
		float q22 = (float)GetHeight( xCeil, yCeil ) / (float)ushort.MaxValue;

		float deltaX = x - xFloor;
		float deltaY = y - yFloor;

		float interpolatedValue = (1 - deltaX) * (1 - deltaY) * q11 +
						   deltaX * (1 - deltaY) * q21 +
						   (1 - deltaX) * deltaY * q12 +
						   deltaX * deltaY * q22;

		return interpolatedValue;
	}
	}
}
