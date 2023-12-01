using System.Text.Json.Serialization;

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
	//
	// Serializable in the best formats for precision
	//
	public ushort[] HeightMap { get; set; }

	public int HeightMapSize { get; set; }

	public float MaxHeight { get; set; }

	// This might be a trap design, should be more generic layers?
	public Color32[] ControlMap { get; set; }

	//
	// Public non-serializable
	//

	[JsonIgnore]
	public Texture HeightmapTexture { get; private set; }

	public TerrainData()
	{
		SetSize( 513 );

		MaxHeight = 1024.0f;
	}

	public void SetSize( int size )
	{
		HeightMap = new ushort[size * size];
		ControlMap = new Color32[size * size];

		for ( int i = 0; i < size * size; i++ )
		{
			ControlMap[i] = new Color32( 0, 0, 0, 0 );
		}

		HeightMapSize = size;
	}

	public void SetSplat( int x, int y, Color32 value )
	{
		var index = y * HeightMapSize + x;
		if ( index < 0 || index >= HeightMap.Length )
		{
			return;
		}

		ControlMap[y * HeightMapSize + x] = value;
	}

	public Color32 GetSplat( int x, int y )
	{
		var index = y * HeightMapSize + x;
		if ( index < 0 || index >= HeightMap.Length )
		{
			return Color32.White;
		}

		return ControlMap[y * HeightMapSize + x];
	}

	public void SetHeight( int x, int y, ushort value )
	{
		var index = y * HeightMapSize + x;
		if ( index < 0 || index >= HeightMap.Length )
		{
			return;
		}

		HeightMap[y * HeightMapSize + x] = value;
	}

	public bool InRange( int x, int y )
	{
		if ( x < 0 || x >= HeightMapSize ) return false;
		if ( y < 0 || y >= HeightMapSize ) return false;
		return true;
	}

	public ushort GetHeight( int x, int y )
	{
		var index = y * HeightMapSize + x;
		if ( index < 0 || index >= HeightMap.Length )
		{
			return 0;
		}

		return HeightMap[y * HeightMapSize + x];
	}

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

	public float GetHeightF( int x, int y )
	{
		return 0.0f;
	}

	public void SetHeight( int x, int y, float value )
	{

	}

	// TerrainData.ImportExport.cs

	public void ImportHeightmap()
	{

	}

	public void ExportHeightmap()
	{

	}
}
