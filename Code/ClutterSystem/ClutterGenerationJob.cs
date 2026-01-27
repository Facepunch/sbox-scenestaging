namespace Sandbox.Clutter;

/// <summary>
/// Defines who owns the generated clutter instances.
/// </summary>
public enum ClutterOwnership
{
	/// <summary>
	/// Component owns instances. Models stored in component's Storage, prefabs saved with scene.
	/// Used for volume mode.
	/// </summary>
	Component,
	
	/// <summary>
	/// GridSystem owns instances. Prefabs are unsaved/hidden, tiles manage cleanup.
	/// Used for infinite streaming mode.
	/// </summary>
	GridSystem
}

/// <summary>
/// Unified job for clutter generation.
/// </summary>
public class ClutterGenerationJob
{
	/// <summary>
	/// The clutter definition containing entries and scatterer.
	/// </summary>
	public required ClutterDefinition Clutter { get; init; }
	
	/// <summary>
	/// Parent GameObject for spawned prefabs.
	/// </summary>
	public required GameObject Parent { get; init; }
	
	/// <summary>
	/// Bounds to scatter within.
	/// </summary>
	public required BBox Bounds { get; init; }
	
	/// <summary>
	/// Random seed for deterministic generation.
	/// </summary>
	public required int Seed { get; init; }
	
	/// <summary>
	/// Who owns the generated instances.
	/// </summary>
	public required ClutterOwnership Ownership { get; init; }
	
	/// <summary>
	/// Layer for batched model rendering.
	/// </summary>
	public ClutterLayer Layer { get; init; }
	
	/// <summary>
	/// Tile data for infinite mode (null for volume mode).
	/// </summary>
	public ClutterTile Tile { get; init; }
	
	/// <summary>
	/// Storage for component-owned model instances
	/// </summary>
	public ClutterGridSystem.ClutterStorage Storage { get; init; }
	
	/// <summary>
	/// Optional callback when job completes (for volume mode progress tracking).
	/// </summary>
	public Action OnComplete { get; init; }
	
	/// <summary>
	/// Execute the generation job.
	/// </summary>
	public void Execute()
	{
		if ( !Parent.IsValid() || Clutter?.Scatterer == null )
			return;

		if ( Tile != null )
		{
			Tile.Destroy();
			Layer?.ClearTileModelInstances( Tile.Coordinates );
		}

		var seed = Tile != null 
			? Scatterer.GenerateSeed( Tile.SeedOffset, Tile.Coordinates.x, Tile.Coordinates.y )
			: Seed;
			
		var instances = Clutter.Scatterer.Scatter( Bounds, Clutter, seed, Parent.Scene );
		if ( instances == null || instances.Count == 0 )
		{
			OnComplete?.Invoke();
			return;
		}

		SpawnInstances( instances );

		if ( Tile != null )
		{
			Tile.IsPopulated = true;
			Layer?.OnTilePopulated( Tile );
		}
		
		OnComplete?.Invoke();
	}

	private void SpawnInstances( List<ClutterInstance> instances )
	{
		var isComponentOwned = Ownership == ClutterOwnership.Component;
		var tileCoord = Tile?.Coordinates ?? Vector2Int.Zero;

		using ( Parent.Scene.Push() )
		{
			foreach ( var instance in instances )
			{
				if ( instance.IsModel && instance.Entry?.Model != null )
				{
					Layer?.AddModelInstance( tileCoord, instance );
					
					// Component ownership: also store in component's storage for persistence
					if ( isComponentOwned && Storage != null )
					{
						Storage.AddInstance(
							instance.Entry.Model.ResourcePath,
							instance.Transform.Position,
							instance.Transform.Rotation,
							instance.Transform.Scale.x
						);
					}
					continue;
				}

				if ( instance.Entry?.Prefab == null )
					continue;

				var obj = instance.Entry.Prefab.Clone( instance.Transform, Parent.Scene );
				obj.Tags.Add( "clutter" );
				obj.SetParent( Parent );

				if ( !isComponentOwned )
				{
					obj.Flags |= GameObjectFlags.NotSaved;
					obj.Flags |= GameObjectFlags.Hidden;
					Tile?.AddObject( obj );
				}
			}
		}
	}
}
