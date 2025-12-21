namespace Sandbox.Clutter;

/// <summary>
/// Unified clutter scattering component supporting both infinite streaming and baked volumes.
/// </summary>
public sealed partial class ClutterComponent : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// The clutter containing objects to scatter and scatter settings.
	/// </summary>
	[Property]
	public ClutterDefinition Clutter { get; set; }

	/// <summary>
	/// Random seed for deterministic generation. Change to get different variations.
	/// </summary>
	[Property]
	public int RandomSeed { get; set; }

	/// <summary>
	/// Enable infinite streaming mode - generates tiles around camera.
	/// Disable for baked volume mode - generates once within bounds.
	/// </summary>
	[Property]
	public bool Infinite
	{
		get => field;
		set
		{
			if ( field == value )
				return;

			// Clear all clutter before switching modes
			Clear();

			field = value;
		}
	}

	/// <summary>
	/// Clears all infinite mode tiles for this component.
	/// </summary>
	public void ClearInfinite()
	{
		var gridSystem = Scene?.GetSystem<ClutterGridSystem>();
		gridSystem?.ClearComponent( this );
	}

	/// <summary>
	/// Invalidates the tile at the given world position, causing it to regenerate.
	/// Only works in infinite mode.
	/// </summary>
	public void InvalidateTileAt( Vector3 worldPosition )
	{
		if ( !Infinite ) return;

		var gridSystem = Scene?.GetSystem<ClutterGridSystem>();
		gridSystem?.InvalidateTileAt( this, worldPosition );
	}

	/// <summary>
	/// Invalidates all tiles within the given bounds, causing them to regenerate.
	/// Only works in infinite mode.
	/// </summary>
	public void InvalidateTilesInBounds( BBox bounds )
	{
		if ( !Infinite ) return;

		var gridSystem = Scene?.GetSystem<ClutterGridSystem>();
		gridSystem?.InvalidateTilesInBounds( this, bounds );
	}

	protected override void OnDisabled()
	{
		Clear();
		base.OnDisabled();
	}

	protected override void DrawGizmos()
	{
		if ( !Infinite )
		{
			DrawVolumeGizmos();
		}
	}
}
