namespace Sandbox.Clutter;

/// <summary>
/// Volume/baked clutter mode - generates once within bounds
/// </summary>
public sealed partial class ClutterComponent
{
	[Property, Group( "Volume" ), ShowIf( nameof( Infinite ), false )]
	public BBox Bounds { get; set; } = new BBox( new Vector3( -512, -512, -128 ), new Vector3( 512, 512, 128 ) );

	[Button( "Generate" )]
	[Icon( "scatter_plot" )]
	public void Generate()
	{
		Clear();

		if ( Clutter == null || Clutter.Scatterer == null )
			return;

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();

		if ( !Infinite )
		{
			var settings = GetCurrentSettings();
			var layer = gridSystem.GetOrCreateLayerForComponent( this, settings );

			var worldBounds = Bounds.Transform( WorldTransform );

			var job = ClutterGenerationJob.Volume(
				bounds: worldBounds,
				cellSize: Clutter.TileSize,
				randomSeed: RandomSeed,
				clutter: Clutter,
				parentObject: GameObject,
				layer: layer
			);

			gridSystem.QueueJob( job );
		}
	}

	[Button( "Clear" )]
	[Icon( "delete" )]
	public void Clear()
	{
		ClearInfinite();

		var children = GameObject.Children.Where( c => c.Tags.Has( "clutter" ) ).ToArray();
		foreach ( var child in children )
			child.Destroy();
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
