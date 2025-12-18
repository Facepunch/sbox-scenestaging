using System;
using System.Collections.Generic;

namespace Sandbox;

/// <summary>
/// Represents a single tile in the clutter spatial grid.
/// Tracks spawned objects for cleanup when the tile is no longer needed.
/// </summary>
public class ClutterTile
{
	/// <summary>
	/// Grid coordinates of this tile.
	/// </summary>
	public Vector2Int Coordinates { get; set; }

	/// <summary>
	/// World-space bounds of this tile.
	/// </summary>
	public BBox Bounds { get; set; }

	/// <summary>
	/// Random seed offset for deterministic generation.
	/// </summary>
	public int SeedOffset { get; set; }

	public bool IsPopulated { get; internal set; }
	
	internal List<GameObject> SpawnedObjects { get; } = [];

	internal void AddObject( GameObject obj )
	{
		if ( obj.IsValid() )
			SpawnedObjects.Add( obj );
	}

	internal void Destroy()
	{
		foreach ( var obj in SpawnedObjects )
			if ( obj.IsValid() ) obj.Destroy();
		
		SpawnedObjects.Clear();
		IsPopulated = false;
	}
}
