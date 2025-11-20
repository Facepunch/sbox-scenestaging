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

	/// <summary>
	/// Generates a hash from all serializable fields and properties using TypeLibrary.
	/// Override this if you need custom hash generation logic.
	/// </summary>
	public override int GetHashCode()
	{
		var hash = new HashCode();
		var typeDesc = TypeLibrary.GetType( GetType() );
		
		if ( typeDesc == null )
			return base.GetHashCode();

		// Hash all properties with [Property] attribute
		foreach ( var property in typeDesc.Properties )
		{
			if ( property.HasAttribute<PropertyAttribute>() )
			{
				var value = property.GetValue( this );
				hash.Add( value?.GetHashCode() ?? 0 );
			}
		}

		return hash.ToHashCode();
	}
}

public class SimpleScatterer : Scatterer
{
	[Property] public RangedFloat Scale { get; set; } = new RangedFloat( 0.8f, 1.2f );
	[Property] public int PointCount { get; set; } = 10;
	
	[Property, Group( "Placement" )]
	public bool PlaceOnGround { get; set; } = true;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public float TraceDistance { get; set; } = 5000f;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public float HeightOffset { get; set; } = 0f;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public bool AlignToNormal { get; set; } = false;

	public override void ScatterInVolume( BBox bounds, ClutterIsotope isotope, GameObject parentObject, Random random )
	{
		if ( isotope == null || parentObject == null )
			return;

		var scene = parentObject.Scene ?? Game.ActiveScene;

		for ( int i = 0; i < PointCount; i++ )
		{
			var point = new Vector3(
				random.Float( bounds.Mins.x, bounds.Maxs.x ),
				random.Float( bounds.Mins.y, bounds.Maxs.y ),
				random.Float( bounds.Mins.z, bounds.Maxs.z )
			);

			if ( PlaceOnGround && scene != null )
			{
				var traceStart = new Vector3( point.x, point.y, bounds.Maxs.z + TraceDistance * 0.5f );
				var traceEnd = new Vector3( point.x, point.y, bounds.Mins.z - TraceDistance * 0.5f );

				var trace = scene.Trace
					.Ray( traceStart, traceEnd )
					.WithoutTags( "player", "trigger", "clutter" )
					.Run();

				if ( trace.Hit )
				{
					point = trace.HitPosition + trace.Normal * HeightOffset;
					
					var yaw = random.Float( 0f, 360f );
					Rotation rotation;
					
					if ( AlignToNormal && trace.Normal != Vector3.Zero )
					{
						rotation = Rotation.From( new Angles( 0, yaw, 0 ) ) * Rotation.FromToRotation( Vector3.Up, trace.Normal );
					}
					else
					{
						rotation = Rotation.FromYaw( yaw );
					}
					
					var scale = random.Float( Scale.x, Scale.y );
					SpawnObjectSimple( point, rotation, scale, isotope, random, parentObject );
				}
			}
			else
			{
				var rotation = Rotation.FromYaw( random.Float( 0f, 360f ) );
				var scale = random.Float( Scale.x, Scale.y );
				SpawnObjectSimple( point, rotation, scale, isotope, random, parentObject );
			}
		}
	}

	public override void Scatter( BBox bounds, ClutterIsotope isotope, ClutterTile tile = null, GameObject parentObject = null )
	{
		var seed = tile != null 
			? HashCode.Combine( tile.Coordinates.x, tile.Coordinates.y, tile.SeedOffset )
			: HashCode.Combine( bounds.Center.x, bounds.Center.y );
		
		var random = new Random( seed );
		var scene = parentObject?.Scene ?? Game.ActiveScene;

		for ( int i = 0; i < PointCount; i++ )
		{
			var point = new Vector3(
				random.Float( bounds.Mins.x, bounds.Maxs.x ),
				random.Float( bounds.Mins.y, bounds.Maxs.y ),
				bounds.Center.z
			);

			if ( PlaceOnGround && scene != null )
			{
				var traceStart = new Vector3( point.x, point.y, bounds.Maxs.z + TraceDistance * 0.5f );
				var traceEnd = new Vector3( point.x, point.y, bounds.Mins.z - TraceDistance * 0.5f );

				var trace = scene.Trace
					.Ray( traceStart, traceEnd )
					.WithoutTags( "player", "trigger", "clutter" )
					.Run();

				if ( trace.Hit )
				{
					point = trace.HitPosition + trace.Normal * HeightOffset;
					
					var yaw = random.Float( 0f, 360f );
					Rotation rotation;
					
					if ( AlignToNormal && trace.Normal != Vector3.Zero )
					{
						rotation = Rotation.From( new Angles( 0, yaw, 0 ) ) * Rotation.FromToRotation( Vector3.Up, trace.Normal );
					}
					else
					{
						rotation = Rotation.FromYaw( yaw );
					}
					
					var scale = random.Float( Scale.x, Scale.y );
					SpawnObject( point, rotation, scale, isotope, random, tile, parentObject );
				}
			}
			else
			{
				var rotation = Rotation.FromYaw( random.Float( 0f, 360f ) );
				var scale = random.Float( Scale.x, Scale.y );
				SpawnObject( point, rotation, scale, isotope, random, tile, parentObject );
			}
		}
	}

	private void SpawnObject( Vector3 position, Rotation rotation, float scale, ClutterIsotope isotope, Random random, ClutterTile tile, GameObject parentObject )
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
			
			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = entry.Model;
			
			spawnedObject = go;
		}

		if ( spawnedObject != null )
		{
			spawnedObject.Tags.Add( "clutter" );
			
			if ( parentObject != null )
			{
				spawnedObject.SetParent( parentObject );
			}
		}

		if ( spawnedObject != null && tile != null )
		{
			tile.AddObject( spawnedObject );
		}
	}

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

		if ( spawnedObject != null )
		{
			spawnedObject.Tags.Add( "clutter" );
			
			if ( parentObject != null )
			{
				spawnedObject.SetParent( parentObject );
			}
		}
	}

	private IsotopeEntry GetRandomEntry( ClutterIsotope isotope, Random random )
	{
		var validEntries = isotope.Entries
			.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
			.ToList();

		if ( validEntries.Count == 0 )
			return null;

		var totalWeight = validEntries.Sum( e => e.Weight );
		var randomValue = random.Float( 0f, totalWeight );

		float cumulativeWeight = 0f;
		foreach ( var entry in validEntries )
		{
			cumulativeWeight += entry.Weight;
			if ( randomValue <= cumulativeWeight )
			{
				return entry;
			}
		}

		return validEntries[^1];
	}
}
