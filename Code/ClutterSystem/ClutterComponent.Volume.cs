namespace Sandbox.Clutter;

/// <summary>
/// Volume/baked clutter mode - generates once within bounds
/// </summary>
public sealed partial class ClutterComponent
{
	[Property, Group( "Volume" ), ShowIf( nameof( Infinite ), false )]
	public BBox Bounds { get; set; } = new BBox( new Vector3( -1024, -512, -1024 ), new Vector3( 1024, 512, 1024 ) );

	/// Storage for volume model instances. Serialized with component.
	/// </summary>
	[Property, Hide]
	public ClutterGridSystem.ClutterStorage Storage { get; set; } = new();

	/// <summary>
	/// Layer used for rendering volume model instances.
	/// </summary>
	private ClutterLayer _volumeLayer;
	[Button( "Generate" )]
	[Icon( "scatter_plot" )]
	public void Generate()
	{
		if ( Infinite ) return;
		
		Clear();

		if ( Clutter == null || Clutter.Scatterer == null )
			return;

		var worldBounds = Bounds.Transform( WorldTransform );
		var instances = Clutter.Scatterer.Scatter( worldBounds, Clutter, Seed, Scene );
		if ( instances == null || instances.Count == 0 )
			return;

		Storage ??= new();
		Storage.ClearAll();
		
		foreach ( var instance in instances )
		{
			if ( !worldBounds.Contains( instance.Transform.Position ) )
				continue;

			if ( instance.Entry?.Model != null )
			{
				Storage.AddInstance(
					instance.Entry.Model.ResourcePath,
					instance.Transform.Position,
					instance.Transform.Rotation,
					instance.Transform.Scale.x
				);
			}
			else if ( instance.Entry?.Prefab != null )
			{
				var obj = instance.Entry.Prefab.Clone( instance.Transform, Scene );
				obj.Tags.Add( "clutter" );
				obj.SetParent( GameObject );
			}
		}

		RebuildVolumeLayer();
	}

	/// <summary>
	/// Rebuilds the visual layer from stored model instances.
	/// Called on scene load and when entering play mode.
	/// </summary>
	internal void RebuildVolumeLayer()
	{
		if ( Storage == null || Storage.TotalCount == 0 )
		{
			_volumeLayer?.ClearAllTiles();
			return;
		}

		var gridSystem = Scene.GetSystem<ClutterGridSystem>();
		if ( gridSystem == null ) return;

		var settings = GetCurrentSettings();
		_volumeLayer ??= gridSystem.GetOrCreateLayerForComponent( this, settings );
		_volumeLayer.ClearAllTiles();

		int addedCount = 0;

		// Rebuild model instances from storage
		foreach ( var modelPath in Storage.ModelPaths )
		{
			var model = ResourceLibrary.Get<Model>( modelPath );
			if ( model == null )
			{
				Log.Warning( $"ClutterComponent: Failed to load model: {modelPath}" );
				continue;
			}

			foreach ( var instance in Storage.GetInstances( modelPath ) )
			{
				_volumeLayer.AddModelInstance( Vector2Int.Zero, new ClutterInstance
				{
					Transform = new Transform( instance.Position, instance.Rotation, instance.Scale ),
					Entry = new ClutterEntry { Model = model }
				} );
				addedCount++;
			}
		}

		if ( addedCount > 0 )
		{
			_volumeLayer.RebuildBatches();
		}
	}

	[Button( "Clear" )]
	[Icon( "delete" )]
	public void Clear()
	{
		ClearInfinite();
		ClearVolume();
	}

	private void ClearVolume()
	{
		// Clear storage
		Storage?.ClearAll();
		
		// Clear layer batches
		_volumeLayer?.ClearAllTiles();

		// Destroy prefab children immediately
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
