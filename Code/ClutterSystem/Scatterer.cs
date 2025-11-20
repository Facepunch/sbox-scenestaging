using Sandbox.Sdf;
using System;
using System.Text.Json.Serialization;

namespace Sandbox;

/// <summary>
/// Base class to override if you want to create custom scatterer logic.
/// </summary>
[JsonDerivedType( typeof(SimpleScatterer), "SimpleScatterer" )]
public abstract class Scatterer
{
	/// <summary>
	/// Will be executed for each tile during scattering. Do whatever you want.
	/// </summary>
	/// <param name="bounds">World-space bounds to scatter within</param>
	/// <param name="isotope">The isotope containing objects to scatter</param>
	/// <param name="tile">Optional tile reference to track spawned objects</param>
	/// <param name="parentObject">GameObject to parent spawned objects to</param>
	public virtual void Scatter( BBox bounds, ClutterIsotope isotope, ClutterTile tile = null, GameObject parentObject = null )
	{
		// Override this to implement custom scattering logic
	}

	/// <summary>
	/// Scatters clutter in a volume without tiles. Used for baked/volume-based clutter.
	/// Override this if your scatterer needs custom volume scattering logic.
	/// </summary>
	public virtual void ScatterInVolume( BBox bounds, ClutterIsotope isotope, GameObject parentObject, Random random )
	{
		// Default implementation calls regular Scatter
		Scatter( bounds, isotope, null, parentObject );
	}
}

public class SimpleScatterer : Scatterer
{
	[Property] public RangedFloat Scale { get; set; } = new RangedFloat( 0.8f, 1.2f );
	[Property] public int PointCount { get; set; } = 10;
	
	[Property, Group( "Placement" )]
	public bool PlaceOnGround { get; set; } = true;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public float TraceDistance { get; set; } = 2000f;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public float HeightOffset { get; set; } = 0f;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public bool AlignToNormal { get; set; } = false;

	/// <summary>
	/// Scatters clutter in a volume without tiles. Used for baked/volume-based clutter.
	/// </summary>
	public override void ScatterInVolume( BBox bounds, ClutterIsotope isotope, GameObject parentObject, Random random )
	{
		if ( isotope == null || parentObject == null )
			return;

		var scene = parentObject.Scene ?? Game.ActiveScene;

		// Generate random spawn points in the volume
		for ( int i = 0; i < PointCount; i++ )
		{
			var point = new Vector3(
				random.Float( bounds.Mins.x, bounds.Maxs.x ),
				random.Float( bounds.Mins.y, bounds.Maxs.y ),
				random.Float( bounds.Mins.z, bounds.Maxs.z )
			);

			// Trace to ground if enabled
			if ( PlaceOnGround && scene != null )
			{
				var traceStart = point + Vector3.Up * (TraceDistance * 0.5f);
				var traceEnd = point + Vector3.Down * (TraceDistance * 0.5f);

				var trace = scene.Trace
					.Ray( traceStart, traceEnd )
					.WithoutTags( "player", "trigger" )
					.Run();

				if ( trace.Hit )
				{
					point = trace.HitPosition + trace.Normal * HeightOffset;
					
					// Calculate rotation
					var rotation = Rotation.FromYaw( random.Float( 0f, 360f ) );
					
					// Optionally align to surface normal
					if ( AlignToNormal )
					{
						rotation = Rotation.LookAt( trace.Normal ) * Rotation.FromYaw( random.Float( 0f, 360f ) );
					}
					
					var scale = random.Float( Scale.x, Scale.y );
					
					// Spawn object at traced position
					SpawnObjectSimple( point, rotation, scale, isotope, random, parentObject );
				}
			}
			else
			{
				// No ground tracing, spawn at generated point
				var rotation = Rotation.FromYaw( random.Float( 0f, 360f ) );
				var scale = random.Float( Scale.x, Scale.y );
				SpawnObjectSimple( point, rotation, scale, isotope, random, parentObject );
			}
		}
	}

	public override void Scatter( BBox bounds, ClutterIsotope isotope, ClutterTile tile = null, GameObject parentObject = null )
	{
		// Create a deterministic random generator based on tile coordinates
		var seed = tile != null 
			? HashCode.Combine( tile.Coordinates.x, tile.Coordinates.y )
			: HashCode.Combine( bounds.Center.x, bounds.Center.y );
		
		var random = new Random( seed );

		// Get scene for tracing
		var scene = parentObject?.Scene ?? Game.ActiveScene;

		// Generate random spawn points
		for ( int i = 0; i < PointCount; i++ )
		{
			var point = new Vector3(
				random.Float( bounds.Mins.x, bounds.Maxs.x ),
				random.Float( bounds.Mins.y, bounds.Maxs.y ),
				bounds.Center.z
			);

			// Trace to ground if enabled
			if ( PlaceOnGround && scene != null )
			{
				var traceStart = point + Vector3.Up * (TraceDistance * 0.5f);
				var traceEnd = point + Vector3.Down * (TraceDistance * 0.5f);

				var trace = scene.Trace
					.Ray( traceStart, traceEnd )
					.WithoutTags( "player", "trigger" )
					.Run();

				if ( trace.Hit )
				{
					point = trace.HitPosition + trace.Normal * HeightOffset;
					
					// Calculate rotation
					var rotation = Rotation.FromYaw( random.Float( 0f, 360f ) );
					
					// Optionally align to surface normal
					if ( AlignToNormal )
					{
						rotation = Rotation.LookAt( trace.Normal ) * Rotation.FromYaw( random.Float( 0f, 360f ) );
					}
					
					var scale = random.Float( Scale.x, Scale.y );
					
					// Spawn object at traced position
					SpawnObject( point, rotation, scale, isotope, random, tile, parentObject );
				}
			}
			else
			{
				// No ground tracing, spawn at generated point
				var rotation = Rotation.FromYaw( random.Float( 0f, 360f ) );
				var scale = random.Float( Scale.x, Scale.y );
				SpawnObject( point, rotation, scale, isotope, random, tile, parentObject );
			}
		}
	}

	private void SpawnObject( Vector3 position, Rotation rotation, float scale, ClutterIsotope isotope, Random random, ClutterTile tile, GameObject parentObject )
	{
		// Get a random weighted entry from the isotope using our seeded random
		var entry = GetRandomEntry( isotope, random );
		if ( entry == null )
			return;

		var transform = new Transform( position, rotation, scale );
		GameObject spawnedObject = null;

		// Spawn prefab or model
		if ( entry.Prefab != null )
		{
			spawnedObject = entry.Prefab.Clone( transform );
		}
		else if ( entry.Model != null )
		{
			var go = new GameObject( true, $"Clutter_{entry.Model.Name}" );
			go.WorldTransform = transform;
			
			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = entry.Model;
			
			spawnedObject = go;
		}

		// Parent to the scatterer's GameObject
		if ( spawnedObject != null && parentObject != null )
		{
			spawnedObject.SetParent( parentObject );
		}

		// Track the spawned object in the tile
		if ( spawnedObject != null && tile != null )
		{
			tile.AddObject( spawnedObject );
		}
	}

	/// <summary>
	/// Simplified spawn without tile tracking. Used for volume-based scatter.
	/// </summary>
	private void SpawnObjectSimple( Vector3 position, Rotation rotation, float scale, ClutterIsotope isotope, Random random, GameObject parentObject )
	{
		var entry = GetRandomEntry( isotope, random );
		if ( entry == null )
			return;

		var transform = new Transform( position, rotation, scale );
		GameObject spawnedObject = null;

		if ( entry.Prefab != null )
		{
			spawnedObject = entry.Prefab.Clone( transform );
		}
		else if ( entry.Model != null )
		{
			var go = new GameObject( true, $"Clutter_{entry.Model.Name}" );
			go.WorldTransform = transform;
			go.Components.Create<ModelRenderer>().Model = entry.Model;
			spawnedObject = go;
		}

		if ( spawnedObject != null && parentObject != null )
		{
			spawnedObject.SetParent( parentObject );
		}
	}

	private IsotopeEntry GetRandomEntry( ClutterIsotope isotope, Random random )
	{
		// Filter to valid entries only
		var validEntries = isotope.Entries
			.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
			.ToList();

		if ( validEntries.Count == 0 )
			return null;

		// Calculate total weight
		var totalWeight = validEntries.Sum( e => e.Weight );
		
		// Generate random value between 0 and total weight using our seeded random
		var randomValue = random.Float( 0f, totalWeight );

		// Find the entry that corresponds to this random value
		float cumulativeWeight = 0f;
		foreach ( var entry in validEntries )
		{
			cumulativeWeight += entry.Weight;
			if ( randomValue <= cumulativeWeight )
			{
				return entry;
			}
		}

		// Fallback
		return validEntries[^1];
	}
}
