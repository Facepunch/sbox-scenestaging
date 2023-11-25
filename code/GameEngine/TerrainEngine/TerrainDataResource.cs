using Sandbox;

/// <summary>
/// The TerrainData class stores heightmaps, detail mesh positions, tree instances, and terrain texture alpha maps.
/// The Terrain component links to the terrain data and renders it.
/// </summary>
[GameResource( "Terrain Data", "terrain", "All the stuff of a terrain", Icon = "landscape" )]
public class TerrainDataResource : GameResource
{
	//
	// None of this should be edited through the editor I guess
	//
	public ushort[] HeightMap { get; private set; }

	public int HeightMapWidth { get; private set; }
	public int HeightMapHeight { get; private set; }

	public float MaxHeight { get; private set; }

	public TerrainDataResource()
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

	protected override void PostLoad()
	{
		base.PostLoad();
	}

}
