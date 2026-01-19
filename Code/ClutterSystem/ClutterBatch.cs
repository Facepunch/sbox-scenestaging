namespace Sandbox.Clutter;

/// <summary>
/// Custom scene object for rendering batched clutter models.
/// Groups instances by model type for efficient GPU instanced rendering.
/// </summary>
internal class ClutterBatch : SceneCustomObject
{
	/// <summary>
	/// Batches organized by model.
	/// </summary>
	private Dictionary<Model, ClutterModelBatch> _batches = [];

	/// <summary>
	/// Combined bounds of all batches for coarse frustum culling.
	/// </summary>
	private BBox _totalBounds;

	public ClutterBatch( SceneWorld world ) : base( world )
	{
		Flags.IsOpaque = true;
		Flags.IsTranslucent = false;
	}

	/// <summary>
	/// Adds a clutter instance to the appropriate batch.
	/// </summary>
	public void AddInstance( ClutterInstance instance )
	{
		if ( instance.Entry?.Model == null )
			return;

		var model = instance.Entry.Model;

		if ( !_batches.TryGetValue( model, out var batch ) )
		{
			batch = new ClutterModelBatch( model );
			_batches[model] = batch;
		}

		batch.AddInstance( instance.Transform );
	}

	/// <summary>
	/// Builds all batches and calculates total bounds.
	/// Call this after adding all instances.
	/// </summary>
	public void Build()
	{
		if ( _batches.Count == 0 )
		{
			Bounds = default;
			return;
		}

		// Calculate combined bounds
		_totalBounds = _batches.Values.First().Bounds;
		foreach ( var batch in _batches.Values.Skip( 1 ) )
		{
			_totalBounds = _totalBounds.AddBBox( batch.Bounds );
		}

		// Set bounds for frustum culling
		Bounds = _totalBounds;
	}

	/// <summary>
	/// Clears all batches.
	/// </summary>
	public void Clear()
	{
		foreach ( var batch in _batches.Values )
			batch.Clear();

		_batches.Clear();
		_totalBounds = default;
		Bounds = default;
	}

	/// <summary>
	/// Called when the batch is deleted. Cleans up resources.
	/// </summary>
	public new void Delete()
	{
		Clear();
		base.Delete();
	}

	/// <summary>
	/// Renders all batched instances using GPU instancing.
	/// </summary>
	public override void RenderSceneObject()
	{
		if ( _batches.Count == 0 )
			return;

		// Render each batch with instancing
		foreach ( var (model, batch) in _batches )
		{
			if ( batch.Transforms.Count == 0 || model == null )
				continue;
			Graphics.DrawModelInstanced( model, [.. batch.Transforms] );
		}
	}
}
