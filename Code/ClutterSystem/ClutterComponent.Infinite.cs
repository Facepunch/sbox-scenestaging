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

	private ClutterLayer _layer;
	private ClutterGridSystem _gridSystem;

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
		_layer = _gridSystem.Register( settings, GameObject );
	}

	private void DisableInfinite()
	{
		if ( _layer == null )
			return;

		_gridSystem.Unregister( _layer );
		_layer = null;
	}

	private void UpdateInfinite()
	{
		if ( _layer == null )
			return;

		_layer.UpdateSettings( GetCurrentSettings() );
	}

	private void RegenerateAllTiles()
	{
		if ( _layer == null )
			return;

		_layer.UpdateSettings( GetCurrentSettings() );
	}
}
