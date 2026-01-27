namespace Sandbox.Clutter;

/// <summary>
/// Clutter scattering component supporting both infinite and volumes.
/// </summary>
[Icon( "forest" )]
public sealed partial class ClutterComponent : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// Clutter generation mode.
	/// </summary>
	public enum ClutterMode
	{
		[Icon( "inventory_2" ), Description( "Scatter clutter within a defined volume" )]
		Volume,
		
		[Icon( "all_inclusive" ), Description( "Stream clutter infinitely around the camera" )]
		Infinite
	}

	/// <summary>
	/// The clutter containing objects to scatter and scatter settings.
	/// </summary>
	[Property]
	public ClutterDefinition Clutter { get; set; }

	/// <summary>
	/// Seed for deterministic generation. Change to get different variations.
	/// </summary>
	[Property]
	public int Seed { get; set; }

	/// <summary>
	/// Clutter generation mode - Volume or Infinite streaming.
	/// </summary>
	[Property]
	public ClutterMode Mode
	{
		get => field;
		set
		{
			if ( field == value ) return;
			Clear();
			field = value;
		}
	}

	/// <summary>
	/// Enable infinite streaming mode.
	/// </summary>
	[Hide]
	public bool Infinite => Mode == ClutterMode.Infinite;

	/// <summary>
	/// Clears all infinite mode tiles for this component.
	/// </summary>
	public void ClearInfinite()
	{
		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		gridSystem?.ClearComponent( this );
	}

	/// <summary>
	/// Invalidates the tile at the given world position, causing it to regenerate.
	/// </summary>
	public void InvalidateTileAt( Vector3 worldPosition )
	{
		if ( Mode != ClutterMode.Infinite ) return;

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		gridSystem.InvalidateTileAt( this, worldPosition );
	}

	/// <summary>
	/// Invalidates all tiles within the given bounds, causing them to regenerate.
	/// </summary>
	public void InvalidateTilesInBounds( BBox bounds )
	{
		if ( Mode != ClutterMode.Infinite ) return;

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		gridSystem.InvalidateTilesInBounds( this, bounds );
	}

	protected override void OnEnabled()
	{
		if ( Mode == ClutterMode.Volume )
		{
			RebuildVolumeLayer();
		}
	}

	protected override void OnDisabled()
	{
		Clear();
	}

	protected override void OnUpdate()
	{
		if ( Mode == ClutterMode.Volume )
		{
			UpdateVolumeProgress();
		}
	}

	protected override void DrawGizmos()
	{
		if ( Mode == ClutterMode.Volume )
		{
			DrawVolumeGizmos();
		}
	}
}
