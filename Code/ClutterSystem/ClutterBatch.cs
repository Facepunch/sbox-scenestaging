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
	/// GPU buffer containing all instance transforms.
	/// </summary>
	private GpuBuffer<Matrix> _transformBuffer;

	/// <summary>
	/// Combined bounds of all batches for coarse frustum culling.
	/// </summary>
	private BBox _totalBounds;

	/// <summary>
	/// Offset into transform buffer for each batch.
	/// </summary>
	private Dictionary<Model, int> _batchOffsets = [];

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
			batch = new ClutterModelBatch { Model = model };
			_batches[model] = batch;
		}

		batch.AddInstance( instance.Transform );
	}

	/// <summary>
	/// Finalizes all batches and calculates total bounds.
	/// Uploads transforms to GPU buffer.
	/// Call this after adding all instances.
	/// </summary>
	public void Finalize()
	{
		if ( _batches.Count == 0 )
		{
			_totalBounds = default;
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

		// Upload transforms to GPU
		UploadTransformsToGpu();
	}

	/// <summary>
	/// Uploads all instance transforms to a GPU buffer and records batch offsets.
	/// </summary>
	private void UploadTransformsToGpu()
	{
		// Collect all transforms and track offsets per batch
		var allTransforms = new List<Matrix>();
		_batchOffsets.Clear();

		foreach ( var (model, batch) in _batches )
		{
			_batchOffsets[model] = allTransforms.Count;

			foreach ( var transform in batch.Transforms )
			{
				// Convert Transform to Matrix (TRS composition)
				var matrix = Matrix.CreateScale( transform.Scale )
					* Matrix.CreateRotation( transform.Rotation )
					* Matrix.CreateTranslation( transform.Position );
				allTransforms.Add( matrix );
			}
		}

		if ( allTransforms.Count == 0 )
			return;

		// Create or resize buffer if needed
		if ( _transformBuffer == null || _transformBuffer.ElementCount != allTransforms.Count )
		{
			_transformBuffer?.Dispose();
			_transformBuffer = new GpuBuffer<Matrix>( allTransforms.Count );
		}

		// Upload to GPU
		_transformBuffer.SetData( allTransforms );
	}

	/// <summary>
	/// Clears all batches and GPU buffers.
	/// </summary>
	public void Clear()
	{
		foreach ( var batch in _batches.Values )
			batch.Clear();

		_batches.Clear();
		_batchOffsets.Clear();

		_transformBuffer?.Dispose();
		_transformBuffer = null;

		_totalBounds = default;
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
		if ( _batches.Count == 0 || _transformBuffer == null )
			return;

		// Render each batch with instancing
		foreach ( var (model, batch) in _batches )
		{
			if ( batch.Transforms.Count == 0 )
				continue;

			var offset = _batchOffsets[model];
			var count = batch.Transforms.Count;

			// Set up render attributes
			var attributes = new RenderAttributes();
			attributes.Set( "TransformBuffer", _transformBuffer );
			attributes.Set( "InstanceOffset", offset );

			// Draw each material in the model instanced
			foreach ( var material in model.Materials )
			{
				if ( material == null )
					continue;

				// Draw instanced using the model's render data
				// TODO: Need to access model's mesh data to draw properly
				// For now, this sets up the infrastructure
			}
		}
	}
}
