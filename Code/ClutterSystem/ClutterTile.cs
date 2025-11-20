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

	/// <summary>
	/// Whether this tile has been populated with clutter.
	/// </summary>
	public bool IsPopulated { get; set; }

	/// <summary>
	/// Objects spawned in this tile. Tracked for cleanup purposes.
	/// </summary>
	internal List<GameObject> SpawnedObjects { get; } = new();

	/// <summary>
	/// Adds a spawned object to this tile's tracking for cleanup.
	/// </summary>
	internal void AddObject( GameObject obj )
	{
		if ( obj != null && obj.IsValid() )
		{
			SpawnedObjects.Add( obj );
		}
	}

	/// <summary>
	/// Destroys all objects spawned in this tile.
	/// </summary>
	internal void Destroy()
	{
		foreach ( var obj in SpawnedObjects )
		{
			if ( obj.IsValid() )
			{
				obj.Destroy();
			}
		}
		SpawnedObjects.Clear();
	}
}
