namespace Sandbox;

/// <summary>
/// Infinite/streaming clutter mode - generates tiles around camera
/// </summary>
public sealed partial class ClutterComponent
{
	[Property, Group( "Infinite" ), ShowIf( nameof(Infinite), true )]
	public float TileSize { get; set; } = 512f;

	[Property, Group( "Infinite" ), ShowIf( nameof(Infinite), true )]
	public int TileRadius { get; set; } = 4;

	internal ClutterSettings GetCurrentSettings()
	{
		if ( Isotope == null )
			return default;

		return new ClutterSettings( Isotope.TileSize, Isotope.TileRadius, RandomSeed, Isotope );
	}
}
