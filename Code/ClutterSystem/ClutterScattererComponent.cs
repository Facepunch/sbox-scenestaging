using Sandbox;

namespace Sandbox;

/// <summary>
/// Component that automatically registers itself with the ClutterGridSystem.
/// Simply add this to a GameObject and assign an Isotope to scatter clutter around the scene.
/// </summary>
public sealed class ClutterScattererComponent : Component
{
	/// <summary>
	/// The isotope containing objects to scatter and scatter settings.
	/// </summary>
	[Property, Group( "Clutter" )]
	public ClutterIsotope Isotope { get; set; }

	/// <summary>
	/// Size of each tile in the grid (world units).
	/// </summary>
	[Property, Group( "Grid Settings" )]
	public float TileSize { get; set; } = 512f;

	/// <summary>
	/// How many tiles to extend the grid in each direction from the center.
	/// Total grid size = (Radius * 2 + 1) tiles.
	/// </summary>
	[Property, Group( "Grid Settings" )]
	public int TileRadius { get; set; } = 4;

	/// <summary>
	/// If true, the scatterer will follow this GameObject's position instead of the camera.
	/// </summary>
	[Property, Group( "Advanced" )]
	public bool UseThisAsCenter { get; set; } = false;

	private ClutterGridSystem.ClutterData registeredData;
	private ClutterGridSystem gridSystem;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		// Get the grid system
		gridSystem = Scene.GetSystem<ClutterGridSystem>();
		if ( gridSystem == null )
		{
			Log.Warning( $"{GameObject.Name}: ClutterGridSystem not found in scene" );
			return;
		}

		if ( Isotope == null )
		{
			Log.Warning( $"{GameObject.Name}: No isotope assigned" );
			return;
		}

		if ( Isotope.Scatterer == null )
		{
			Log.Warning( $"{GameObject.Name}: Isotope has no scatterer assigned" );
			return;
		}

		// Use the isotope's scatterer
		registeredData = gridSystem.Register( Isotope, Isotope.Scatterer, GameObject, TileSize, TileRadius );

		Log.Info( $"{GameObject.Name}: Clutter registered (tile size: {TileSize}, radius: {TileRadius})" );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		// Unregister when disabled
		if ( registeredData != null && gridSystem != null )
		{
			gridSystem.Unregister( registeredData );
			registeredData = null;
		}
	}

	protected override void OnUpdate()
	{
		if ( registeredData == null )
			return;

		// If we want to use this GameObject's position as the center
		if ( UseThisAsCenter )
		{
			registeredData.Center = WorldPosition;
		}
	}
}
