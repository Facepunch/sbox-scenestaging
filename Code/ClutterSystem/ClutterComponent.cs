namespace Sandbox.Clutter;

/// <summary>
/// Clutter scattering component supporting both infinite and volumes.
/// </summary>
public sealed partial class ClutterComponent : Component, Component.ExecuteInEditor
{
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
	/// Enable infinite streaming mode
	/// Disable for baked volume mode
	/// </summary>
	[Property]
	public bool Infinite
	{
		get => field;
		set
		{
			if ( field == value )
				return;

			Clear();

			field = value;
		}
	}

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
		if ( !Infinite ) return;

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		gridSystem.InvalidateTileAt( this, worldPosition );
	}

	/// <summary>
	/// Invalidates all tiles within the given bounds, causing them to regenerate.
	/// </summary>
	public void InvalidateTilesInBounds( BBox bounds )
	{
		if ( !Infinite ) return;

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		gridSystem.InvalidateTilesInBounds( this, bounds );
	}

	protected override void OnEnabled()
	{
		if ( !Infinite )
		{
			RebuildVolumeLayer();
		}
	}

	protected override void OnDisabled()
	{
		Clear();
	}

	protected override void DrawGizmos()
	{
		if ( !Infinite )
		{
			DrawVolumeGizmos();
		}
	}
}
