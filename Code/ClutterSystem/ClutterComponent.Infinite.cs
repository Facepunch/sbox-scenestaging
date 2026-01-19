namespace Sandbox.Clutter;

/// <summary>
/// Infinite/streaming clutter mode
/// </summary>
public sealed partial class ClutterComponent
{
	internal ClutterSettings GetCurrentSettings()
	{
		if ( Clutter == null )
			return default;

		return new ClutterSettings( Seed, Clutter );
	}
}
