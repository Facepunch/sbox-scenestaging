using Sandbox.Sdf;
using System;
using System.Linq;
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
	public float HeightOffset { get; set; } = 0f;
	
	[Property, Group( "Placement" ), ShowIf( nameof(PlaceOnGround), true )]
	public bool AlignToNormal { get; set; } = false;

	public override void ScatterInVolume( BBox bounds, ClutterIsotope isotope, GameObject parentObject, Random random )
	{
		// Volume mode uses the provided random instance
		ScatterInternal( bounds, isotope, null, parentObject, random );
	}

	public override void Scatter( BBox bounds, ClutterIsotope isotope, ClutterTile tile = null, GameObject parentObject = null )
	{
		int seed = 0;
		if ( tile != null )
		{
			seed = tile.Coordinates.x;
			seed = (seed * 397) ^ tile.Coordinates.y;
			seed = (seed * 397) ^ tile.SeedOffset;
		}
		
		var random = new Random( seed );
		ScatterInternal( bounds, isotope, tile, parentObject, random );
	}

	private void ScatterInternal( BBox bounds, ClutterIsotope isotope, ClutterTile tile, GameObject parentObject, Random random )
	{
		if ( isotope == null || parentObject == null )
			return;

		var scene = parentObject.Scene ?? Game.ActiveScene;

		for ( int i = 0; i < PointCount; i++ )
		{
			var randomX = random.Float( 0f, 1f );
			var randomY = random.Float( 0f, 1f );
			
			var point = new Vector3(
				bounds.Mins.x + randomX * bounds.Size.x,
				bounds.Mins.y + randomY * bounds.Size.y,
				0f
			);

			if ( PlaceOnGround && scene != null )
			{
				var traceStart = new Vector3( point.x, point.y, 10000f );
				var traceEnd = new Vector3( point.x, point.y, -10000f );

				var trace = scene.Trace
					.Ray( traceStart, traceEnd )
					.WithoutTags( "player", "trigger", "clutter" )
					.Run();

				if ( trace.Hit )
				{
					point = trace.HitPosition + trace.Normal * HeightOffset;
					var rotation = CalculateRotation( random, trace.Normal );
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

	private Rotation CalculateRotation( Random random, Vector3 surfaceNormal )
	{
		var yaw = random.Float( 0f, 360f );
		
		if ( AlignToNormal )
		{
			return Rotation.From( new Angles( 0, yaw, 0 ) ) * Rotation.FromToRotation( Vector3.Up, surfaceNormal );
		}
		
		return Rotation.FromYaw( yaw );
	}

	private void SpawnObject( Vector3 position, Rotation rotation, float scale, ClutterIsotope isotope, Random random, ClutterTile tile, GameObject parentObject )
	{
		var entry = GetRandomEntry( isotope, random );
		if ( entry == null )
			return;

		Transform transform = new ( position, rotation, scale );
		GameObject spawnedObject = null;

		if ( entry.Prefab != null )
		{
			spawnedObject = entry.Prefab.Clone( transform );
		}
		else if ( entry.Model != null )
		{
			var go = new GameObject( true );
			go.WorldTransform = transform;
			go.Components.Create<ModelRenderer>().Model = entry.Model;
			spawnedObject = go;
		}

		if ( spawnedObject != null )
		{
			spawnedObject.Flags |= GameObjectFlags.NotSaved;
			spawnedObject.Tags.Add( "clutter" );
			spawnedObject.SetParent( parentObject );
			tile?.AddObject( spawnedObject );
		}
	}

	private IsotopeEntry GetRandomEntry( ClutterIsotope isotope, Random random )
	{
		var validEntries = isotope.Entries
			.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
			.OrderBy( e => isotope.Entries.IndexOf( e ) )
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
