using System;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Volume/baked clutter mode - generates once within bounds
/// </summary>
public sealed partial class ClutterComponent
{
	[Property, Group( "Volume" ), ShowIf( nameof( Infinite ), false )]
	public BBox Bounds { get; set; } = new BBox( -100, 100 );

	[Property, Group( "Volume Info" ), ShowIf( nameof( Infinite ), false ), ReadOnly]
	public int SpawnedCount { get; private set; }

	[Button( "Generate Clutter" ), Group( "Volume" ), ShowIf( nameof( Infinite ), false )]
	[Icon( "scatter_plot" )]
	public void GenerateVolume()
	{
		ClearVolume();

		if ( !IsValid )
			return;

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		var worldBounds = Bounds.Transform( WorldTransform );

		var job = ClutterGenerationJob.Volume(
			bounds: worldBounds,
			cellSize: Clutter.TileSize,
			randomSeed: RandomSeed,
			clutter: Clutter,
			parentObject: GameObject,
			onComplete: count =>
			{
				SpawnedCount = count;
			}
		);

		gridSystem.QueueJob( job );
	}

	[Button( "Clear Clutter" )]
	[Icon( "delete" )]
	public void ClearVolume()
	{
		var children = GameObject.Children.Where( c => c.Tags.Has( "clutter" ) ).ToArray();
		foreach ( var child in children )
			child.Destroy();

		SpawnedCount = 0;
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
