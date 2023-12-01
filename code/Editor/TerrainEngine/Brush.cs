namespace Editor.TerrainEngine;

public class Brush
{
	public Texture Texture { get; private set; }
	public Color32[] Pixels { get; private set; }

	[Range( 1, 1024 )]
	[Property] public int Radius { get; set; } = 128;
	[Range( 0, 100.0f )]
	[Property] public float Strength { get; set; } = 100.0f;

	public void Set( string name )
	{
		Texture = Texture.Load( FileSystem.Mounted, $"Brushes/{name}.png" );
		Pixels = Texture.GetPixels();
	}
}
