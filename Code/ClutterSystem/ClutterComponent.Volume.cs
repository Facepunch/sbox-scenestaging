using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Volume/baked clutter mode - generates once within bounds
/// </summary>
public sealed partial class ClutterComponent
{
	[Property, Group( "Volume" ), ShowIf( nameof(Infinite), false )]
	public BBox Bounds { get; set; } = new BBox( -100, 100 );

	[Property, Group( "Volume" ), ShowIf( nameof(Infinite), false )]
	public float CellSize { get; set; } = 512f;

	[Property, Group( "Volume Info" ), ShowIf( nameof(Infinite), false ), ReadOnly]
	public int SpawnedCount { get; private set; }

	[Property, Group( "Volume Info" ), ShowIf( nameof(Infinite), false ), ReadOnly]
	public int CellCount { get; private set; }

	[Button( "Generate Clutter" ), Group( "Volume" ), ShowIf( nameof(Infinite), false )]
	[Icon( "scatter_plot" )]
	public void GenerateVolume()
	{
		ClearVolume();

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

		var worldBounds = Bounds.Transform( WorldTransform );

		var cellsX = (int)MathF.Max( 1, MathF.Ceiling( worldBounds.Size.x / CellSize ) );
		var cellsY = (int)MathF.Max( 1, MathF.Ceiling( worldBounds.Size.y / CellSize ) );
		var cellsZ = (int)MathF.Max( 1, MathF.Ceiling( worldBounds.Size.z / CellSize ) );

		CellCount = cellsX * cellsY * cellsZ;

		int totalSpawned = 0;
		for ( int x = 0; x < cellsX; x++ )
		{
			for ( int y = 0; y < cellsY; y++ )
			{
				for ( int z = 0; z < cellsZ; z++ )
				{
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
					var cellSeed = HashCode.Combine( RandomSeed, x, y, z );
					var random = new Random( cellSeed );
					var countBefore = GameObject.Children.Count;

					Isotope.Scatterer.ScatterInVolume( cellBounds, Isotope, GameObject, random );

					totalSpawned += GameObject.Children.Count - countBefore;
				}
			}
		}

		SpawnedCount = totalSpawned;
	}

	[Button( "Clear Clutter" ), Group( "Volume" ), ShowIf( nameof(Infinite), false )]
	[Icon( "delete" )]
	public void ClearVolume()
	{
		var children = GameObject.Children.ToArray();
		foreach ( var child in children )
		{
			child.Destroy();
		}

		SpawnedCount = 0;
		CellCount = 0;
	}

	private void DrawVolumeGizmos()
	{
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
