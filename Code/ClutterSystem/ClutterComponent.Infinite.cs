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

	private ClutterGridSystem.ClutterRegistration _infiniteData;
	private ClutterGridSystem _gridSystem;
	private int _lastSettingsHash;

	private ClutterSettings GetCurrentSettings()
	{
		return new ClutterSettings( TileSize, TileRadius, RandomSeed, Isotope );
	}

	private void EnableInfinite()
	{
		var settings = GetCurrentSettings();
		if ( !settings.IsValid )
		{
			Log.Warning( $"{GameObject.Name}: No isotope/scatterer assigned" );
			return;
		}

		_gridSystem = Scene.GetSystem<ClutterGridSystem>();
		_infiniteData = _gridSystem.Register( settings, GameObject );
		_lastSettingsHash = settings.GetHashCode();
	}

	private void DisableInfinite()
	{
		if ( _infiniteData == null )
			return;

		_gridSystem.Unregister( _infiniteData );
		_infiniteData = null;
	}

	private void UpdateInfinite()
	{
		if ( _infiniteData == null )
			return;

		var currentHash = GetCurrentSettings().GetHashCode();
		if ( currentHash != _lastSettingsHash )
		{
			RegenerateAllTiles();
		}
	}

	private void RegenerateAllTiles()
	{
		foreach ( var tile in _infiniteData.Tiles.Values.ToArray() )
		{
			tile.Destroy();
		}
		_infiniteData.Tiles.Clear();

		var settings = GetCurrentSettings();
		_infiniteData.Settings = settings;
		_lastSettingsHash = settings.GetHashCode();
	}
}
