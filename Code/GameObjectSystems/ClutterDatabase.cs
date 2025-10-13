namespace Sandbox;

internal class ClutterDatabase
{
	// TODO: Make this configurable maybe
	private Scene _scene;
	private const int NUM_TIERS = 2;

	public int TierCount => NUM_TIERS;
	public bool IsEmpty => clutterInstances.All( tierList => tierList.Count == 0 );

	// List of clutter instances per tier
	public List<List<ClutterInstance>> clutterInstances = [];

	// Render batch of tier by: tier x model x transform
	private List<Dictionary<Model, Transform[]>> renderbatches = [];

	public ClutterDatabase( Scene scene )
	{
		_scene = scene;

		for ( int i = 0; i < NUM_TIERS; i++ )
		{
			clutterInstances.Add( [] );
			renderbatches.Add( [] );
		}
	}

	public List<ClutterInstance> GetInstancesByTier( int tier )
	{
		if ( tier < 0 || tier >= NUM_TIERS )
		{
			return null;
		}

		return clutterInstances[tier];
	}

	/// <summary>
	/// Gets all instances of the cell. Should avoid using this as much as possible
	/// </summary>
	/// <returns></returns>
	public List<ClutterInstance> GetAllInstances()
	{
		return [.. clutterInstances.SelectMany( tierList => tierList )];
	}

	public void AddInstance( ClutterInstance instance )
	{
		// TODO: Replace is small by tier num
		int tier = instance.IsSmall ? 1 : 0;
		clutterInstances[tier].Add( instance );
	}

	public void RemoveInstance( ClutterInstance instance )
	{
		// TODO: Replace is small by tier num
		int tier = instance.IsSmall ? 1 : 0;
		clutterInstances[tier].Remove( instance );
	}

	/// <summary>
	/// Removes multiple instances from the database
	/// </summary>
	public void RemoveInstances( List<ClutterInstance> instancesToRemove )
	{
		var instanceIds = new HashSet<Guid>( instancesToRemove.Select( i => i.InstanceId ) );
		for ( int tier = 0; tier < NUM_TIERS; tier++ )
		{
			clutterInstances[tier].RemoveAll( i => instanceIds.Contains( i.InstanceId ) );
		}
	}

	/// <summary>
	/// Nuclear solution to clear a cell database.
	/// </summary>
	public void Clear()
	{
		for(int tier = 0; tier < NUM_TIERS; tier++ )
		{
			clutterInstances[tier].Clear();
			renderbatches[tier].Clear();
		}
	}

	/// <summary>
	/// Rebuilds the render batches for all tiers, optionally adjusting heights to terrain
	/// Note: I'm unsure this is the right place to adjust for terrain, but doing it all in one passes avoids use reiterating over everything just for this.
	/// </summary>
	public void RebuildRenderBatches( bool adjustToTerrain = false )
	{
		for ( int tier = 0; tier < NUM_TIERS; tier++ )
		{
			var tierInstances = clutterInstances[tier];
			var renderBatch = renderbatches[tier];

			// Clear existing batches before rebuilding
			renderBatch.Clear();

			for ( int i = 0; i < tierInstances.Count; i++ )
			{
				var instance = tierInstances[i];
				if ( instance.ClutterType is not ClutterInstance.Type.Model || instance.model == null )
					continue;

				// allocates or resize, potential waste of memory by assuming tierINstances.count is the size we want.
				if ( !renderBatch.TryGetValue( instance.model, out var transforms ) || transforms.Length < tierInstances.Count )
				{
					renderBatch[instance.model] = new Transform[tierInstances.Count];
				}

				var transform = instance.transform;
				if ( adjustToTerrain )
				{
					// Cast from the sky to sample ground
					var pos = transform.Position;
					var startPos = new Vector3( pos.x, pos.y, pos.z + 1000f );
					var endPos = new Vector3( pos.x, pos.y, pos.z - 1000f );

					var trace = _scene.Trace.Ray( startPos, endPos )
						.WithTag( "solid" )
						.WithoutTags( "scattered_object" )
						.Run();

					if ( !trace.Hit )
						continue;

					// Update position to new terrain height
					transform.Position = trace.HitPosition;
					instance.transform = transform;

					// Update the instance in the tier list
					tierInstances[i] = instance;

					// Update the instance in all layers that contain it
					UpdateInstanceInLayers( instance );
				}

				renderBatch[instance.model][i] = transform;
			}
		}
	}

	/// <summary>
	/// Returns a dictionary stored by Model, containing a list of transform for instancing
	/// </summary>
	public Dictionary<Model, Transform[]> GetRenderBatches( int tier )
	{
		return renderbatches[tier];
	}

	/// <summary>
	/// Updates an instance in all layers that contain it
	/// </summary>
	private void UpdateInstanceInLayers( ClutterInstance updatedInstance )
	{
		var clutterComponents = _scene.GetAllComponents<ClutterComponent>();
		foreach ( var component in clutterComponents )
		{
			foreach ( var layer in component.Layers )
			{
				// Find and update the instance in the layer by InstanceId
				for ( int i = 0; i < layer.Instances.Count; i++ )
				{
					if ( layer.Instances[i].InstanceId == updatedInstance.InstanceId )
					{
						layer.Instances[i] = updatedInstance;
						break;
					}
				}
			}
		}
	}
}
