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
		return new ClutterSettings( TileSize, TileRadius, RandomSeed, Isotope );
	}

	private void EnableInfinite()
	{
		// Component is now passive - grid system will find it
	}

	private void DisableInfinite()
	{
		// Component is now passive - grid system will clean up
	}

	private void UpdateInfinite()
	{
		// Component is now passive - grid system handles updates
	}

	private void RegenerateAllTiles()
	{
		// Trigger handled by OnValidate -> grid system will detect change
	}
}
