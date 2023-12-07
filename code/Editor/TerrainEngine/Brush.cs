namespace Editor.TerrainEngine;

public class Brush
{
	public Texture Texture { get; private set; }
	public Color32[] Pixels { get; private set; }

	[Range( 8, 1024 )]
	[Property] public int Size { get; set; } = 512;
	[Range( 0, 1.0f, 0.01f )]
	[Property] public float Opacity { get; set; } = 1.0f;

	public void Set( string name )
	{
		Texture = Texture.Load( FileSystem.Mounted, $"Brushes/{name}.png" );
		Pixels = Texture.GetPixels();
	}
}
