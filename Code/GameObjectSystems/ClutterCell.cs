namespace Sandbox;

public class ClutterCell : SceneCustomObject
{
	private readonly Scene _scene;

	/// <summary>
	/// Position in the grid
	/// </summary>
	private Vector2 CellPosition { get; set; }

	/// <summary>
	/// Size in world units
	/// </summary>
	private int CellSize { get; set; }

	/// <summary>
	/// Holds all instances data, sorted by tier
	/// </summary>
	private ClutterDatabase database;

	/// <summary>
	/// The distance at which tier 1 clutter will be culled
	/// </summary>
	private float SmallClutterCullDistance = 4096f;

	public ClutterCell( Scene scene, Vector2 cellPosition, int cellSize ) : base( scene.SceneWorld )
	{
		_scene = scene;

		CellPosition = cellPosition;
		CellSize = cellSize;

		database = new ClutterDatabase( scene );

		Flags.WantsPrePass = true;
		Flags.IsOpaque = true;
	}

	public bool IsEmpty() => database.IsEmpty;

	/// <summary>
	/// Gets the world space bounds of this cell
	/// </summary>
	public BBox GetCellBounds()
	{
		Vector3 worldMin = new( CellPosition.x * CellSize, CellPosition.y * CellSize, -10000 );
		Vector3 worldMax = new( (CellPosition.x + 1) * CellSize, (CellPosition.y + 1) * CellSize, 10000 );
		return new BBox( worldMin, worldMax );
	}

	/// <summary>
	/// Add a clutter instance to this cell
	/// </summary>
	/// <param name="instance"></param>
	internal void AddInstance( ClutterInstance instance )
	{
		database.AddInstance( instance );
	}

	/// <summary>
	/// Rebuild the render batches for this cell, must be executed after a change
	/// </summary>
	internal void RefreshDatabase()
	{
		database.RebuildRenderBatches();
	}

	/// <summary>
	/// Remove a clutter instance from this cell and rebuilt it
	/// </summary>
	/// <param name="instance"></param>
	internal void RemoveInstance( ClutterInstance instance )
	{
		database.RemoveInstance( instance );
		database.RebuildRenderBatches();
	}

	/// <summary>
	/// Remove multiple clutter instances from this cell
	/// </summary>
	internal void RemoveInstances( List<ClutterInstance> instancesToRemove )
	{
		database.RemoveInstances( instancesToRemove );
	}

	/// <summary>
	/// Clear all instances from this cell
	/// </summary>
	internal void ClearInstances()
	{
		database.Clear();
		database.RebuildRenderBatches();
	}

	/// <summary>
	/// Reposition instances in this cell when the terrain has been modified
	/// </summary>
	internal void OnTerrainModified()
	{
		database.RebuildRenderBatches( adjustToTerrain: true );
	}

	/// <summary>
	/// Gets all instances in this cell across all tiers
	/// </summary>
	public List<ClutterInstance> GetAllInstances()
	{
		return database.GetAllInstances();
	}

	/// <summary>
	/// Renders all model instances in this cell in a single draw call
	/// </summary>
	public override void RenderSceneObject()
	{
		if ( database.IsEmpty )
			return;

		var cellBounds = GetCellBounds();
		var cameraPos = Graphics.CameraPosition;
		var distanceToCamera = Vector3.DistanceBetween( cameraPos, cellBounds.Center );

		bool isCellVisible = Graphics.Frustum.IsInside( cellBounds, true );
		if ( Graphics.LayerType == SceneLayerType.Opaque && isCellVisible )
		{
			// Disable shadows for distant cells (beyond 4096 units)
			// TODO: un-hardcode this
			const float ShadowDistanceThreshold = 4096;
			bool castShadows = distanceToCamera < ShadowDistanceThreshold;
			Flags.CastShadows = castShadows;
		}

		// Only draw small instances if camera is close enough and cell is in frustum
		if ( isCellVisible )
		{
			// Always draw large instances( tier 0 )
			foreach ( var batch in database.GetRenderBatches( 0 ) )
			{
				Graphics.DrawModelInstanced( batch.Key, batch.Value );
			}

			// Cull small instances (tier 1) based on distance
			bool withinCullDistance = distanceToCamera < SmallClutterCullDistance;
			if ( withinCullDistance )
			{
				foreach ( var batch in database.GetRenderBatches( 1 ) )
				{
					Graphics.DrawModelInstanced( batch.Key, batch.Value );
				}
			}
		}
	}
}
