namespace Sandbox;

/// <summary>
/// Infinite/streaming clutter mode - generates tiles around camera
/// </summary>
public sealed partial class ClutterComponent
{
	internal ClutterSettings GetCurrentSettings()
	{
		if ( clutter == null )
			return default;

		return new ClutterSettings( clutter.TileSize, clutter.TileRadius, RandomSeed, clutter );
	}
}
