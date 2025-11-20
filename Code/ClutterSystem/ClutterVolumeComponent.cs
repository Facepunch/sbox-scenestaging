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

		// Bounds are in local space, transform to world space for scattering
		var worldBounds = Bounds.Transform( WorldTransform );

		// Use deterministic random seed
		var random = new Random( RandomSeed );

		// Scatter in volume using the isotope's scatterer
		Isotope.Scatterer.ScatterInVolume( worldBounds, Isotope, GameObject, random );

		// Count spawned objects
		SpawnedCount = GameObject.Children.Count;

		Log.Info( $"{GameObject.Name}: Generated {SpawnedCount} clutter objects in volume" );
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
