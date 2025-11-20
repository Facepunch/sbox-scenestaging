using Sandbox;

namespace Sandbox;

/// <summary>
/// Scatters clutter within a defined volume bounds. Objects are spawned once and saved in the scene.
/// Use for forests, fields, or any static scattered area.
/// This is BAKED clutter - generated in editor and saved in the scene.
/// </summary>
public sealed class ClutterVolumeComponent : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// The isotope containing objects to scatter and scatter settings.
	/// </summary>
	[Property, Group( "Clutter" )]
	public ClutterIsotope Isotope { get; set; }

	/// <summary>
	/// Local-space bounds where clutter will be scattered.
	/// </summary>
	[Property, Group( "Volume" )]
	public BBox Bounds { get; set; } = new BBox( -100, 100 );

	/// <summary>
	/// Random seed for deterministic generation. Change to get different layouts.
	/// </summary>
	[Property, Group( "Volume" )]
	public int RandomSeed { get; set; } = 12345;

	/// <summary>
	/// Number of objects currently spawned in this volume (read-only).
	/// </summary>
	[Property, Group( "Info" ), ReadOnly]
	public int SpawnedCount { get; private set; }

	/// <summary>
	/// Generates clutter in the volume. Clears existing clutter first.
	/// </summary>
	[Button( "Generate Clutter" )]
	[Icon( "scatter_plot" )]
	public void Generate()
	{
		Clear();

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

		// Transform bounds to world space
		var worldBounds = Bounds.Transform( WorldTransform );

		// Calculate cell grid dimensions
		var cellsX = (int)MathF.Max( 1, MathF.Ceiling( worldBounds.Size.x / CellSize ) );
		var cellsY = (int)MathF.Max( 1, MathF.Ceiling( worldBounds.Size.y / CellSize ) );
		var cellsZ = (int)MathF.Max( 1, MathF.Ceiling( worldBounds.Size.z / CellSize ) );

		CellCount = cellsX * cellsY * cellsZ;

		Log.Info( $"{GameObject.Name}: Generating clutter in {cellsX}x{cellsY}x{cellsZ} = {CellCount} cells" );

		// Scatter in each cell
		int totalSpawned = 0;
		for ( int x = 0; x < cellsX; x++ )
		{
			for ( int y = 0; y < cellsY; y++ )
			{
				for ( int z = 0; z < cellsZ; z++ )
				{
					// Calculate cell bounds
					var cellMin = new Vector3(
						worldBounds.Mins.x + (x * CellSize),
						worldBounds.Mins.y + (y * CellSize),
						worldBounds.Mins.z + (z * CellSize)
					);

					var cellMax = new Vector3(
						MathF.Min( worldBounds.Maxs.x, cellMin.x + CellSize ),
						MathF.Min( worldBounds.Maxs.y, cellMin.y + CellSize ),
						MathF.Min( worldBounds.Maxs.z, cellMin.z + CellSize )
					);

					var cellBounds = new BBox( cellMin, cellMax );

					// Create deterministic random for this cell (like tiles in grid system)
					var cellSeed = HashCode.Combine( RandomSeed, x, y, z );
					var random = new Random( cellSeed );

					// Track objects spawned before
					var countBefore = GameObject.Children.Count;

					// Scatter in this cell
					Isotope.Scatterer.ScatterInVolume( cellBounds, Isotope, GameObject, random );

					// Count objects spawned in this cell
					totalSpawned += GameObject.Children.Count - countBefore;
				}
			}
		}

		SpawnedCount = totalSpawned;

		Log.Info( $"{GameObject.Name}: Generated {SpawnedCount} clutter objects in {CellCount} cells (avg {SpawnedCount / (float)CellCount:F1} per cell)" );
	}

	/// <summary>
	/// Clears all spawned clutter from this volume.
	/// </summary>
	[Button( "Clear Clutter" )]
	[Icon( "delete" )]
	public void Clear()
	{
		// Destroy all children
		var children = GameObject.Children.ToArray();
		foreach ( var child in children )
		{
			child.Destroy();
		}

		SpawnedCount = 0;
	}

	protected override void DrawGizmos()
	{
		// Draw in local space since Bounds is a local-space BBox
		using ( Gizmo.Scope( "volume" ) )
		{
			Gizmo.Draw.Color = Color.Green.WithAlpha( 0.3f );
			Gizmo.Draw.LineBBox( Bounds );

			if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = Color.Green.WithAlpha( 0.05f );

				if ( Gizmo.Control.BoundingBox( "bounds", Bounds, out var newBounds ) )
				{
					Bounds = newBounds;
				}
			}
		}
	}
}
