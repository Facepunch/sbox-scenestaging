namespace Sandbox;

/// <summary>
/// Infinite/streaming clutter mode - generates tiles around camera
/// </summary>
public sealed partial class ClutterComponent
{
	internal ClutterSettings GetCurrentSettings()
	{
		if ( Clutter == null )
			return default;

		return new ClutterSettings( RandomSeed, Clutter );
	}
}
