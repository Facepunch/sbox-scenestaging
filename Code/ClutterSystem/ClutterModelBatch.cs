using System.Collections.Generic;

namespace Sandbox.Clutter;

/// <summary>
/// Groups multiple instances of the same model for efficient batch rendering.
/// </summary>
public class ClutterModelBatch
{
	/// <summary>
	/// The model being rendered in this batch.
	/// </summary>
	public Model Model { get; set; }

	/// <summary>
	/// List of transforms for each instance.
	/// </summary>
	public List<Transform> Transforms { get; set; } = new();

	/// <summary>
	/// Cached world-space bounds for frustum culling.
	/// Updated when transforms change.
	/// </summary>
	public BBox Bounds { get; set; }

	/// <summary>
	/// Adds an instance to this batch.
	/// </summary>
	public void AddInstance( Transform transform )
	{
		Transforms.Add( transform );
		UpdateBounds( transform );
	}

	/// <summary>
	/// Clears all instances from this batch.
	/// </summary>
	public void Clear()
	{
		Transforms.Clear();
		Bounds = default;
	}

	/// <summary>
	/// Updates the bounds to include the given transform.
	/// </summary>
	private void UpdateBounds( Transform transform )
	{
		if ( Model == null )
			return;

		var modelBounds = Model.Bounds.Transform( transform );
		
		if ( Transforms.Count == 1 )
		{
			Bounds = modelBounds;
		}
		else
		{
			Bounds = Bounds.AddBBox( modelBounds );
		}
	}

	/// <summary>
	/// Recalculates bounds from all transforms.
	/// </summary>
	public void RecalculateBounds()
	{
		if ( Model == null || Transforms.Count == 0 )
		{
			Bounds = default;
			return;
		}

		Bounds = Model.Bounds.Transform( Transforms[0] );
		
		for ( int i = 1; i < Transforms.Count; i++ )
		{
			Bounds = Bounds.AddBBox( Model.Bounds.Transform( Transforms[i] ) );
		}
	}
}
