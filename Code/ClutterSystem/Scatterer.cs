using Sandbox.Sdf;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sandbox.Clutter;

/// <summary>
/// Represents a single clutter instance to be spawned.
/// </summary>
public struct ClutterInstance
{
	public Transform Transform { get; set; }
	public ClutterEntry Entry { get; set; }

	public readonly bool IsModel => Entry?.Model != null && Entry?.Prefab == null;
}

/// <summary>
/// Base class to override if you want to create custom scatterer logic.
/// Provides utility methods for entry selection and common operations.
/// </summary>
[JsonDerivedType( typeof( SimpleScatterer ), "SimpleScatterer" )]
[JsonDerivedType( typeof( SlopeScatterer ), "SlopeScatterer" )]
[JsonDerivedType( typeof( TerrainMaterialScatterer ), "TerrainMaterialScatterer" )]
public abstract class Scatterer
{
	/// <summary>
	/// Random instance for this scattering operation.
	/// </summary>
	[JsonIgnore]
	[Hide]
	protected Random Random { get; private set; }

	/// <summary>
	/// Generates clutter instances for the given bounds.
	/// The Random property is initialized before this is called.
	/// </summary>
	/// <param name="bounds">World-space bounds to scatter within</param>
	/// <param name="clutter">The clutter containing objects to scatter</param>
	/// <param name="scene">Scene to use for tracing (null falls back to Game.ActiveScene)</param>
	/// <returns>Collection of clutter instances to spawn</returns>
	protected abstract List<ClutterInstance> Generate( BBox bounds, ClutterDefinition clutter, Scene scene = null );

	/// <summary>
	/// Public entry point for scattering. Creates Random from seed and calls Generate().
	/// </summary>
	/// <param name="bounds">World-space bounds to scatter within</param>
	/// <param name="clutter">The clutter containing objects to scatter</param>
	/// <param name="seed">Seed for deterministic random generation</param>
	/// <param name="scene">Scene to use for tracing (required in editor mode)</param>
	/// <returns>Collection of clutter instances to spawn</returns>
	public List<ClutterInstance> Scatter( BBox bounds, ClutterDefinition clutter, int seed, Scene scene = null )
	{
		// Let's make a deterministic Random object for scatterer to use.
		Random = new Random( seed );

		return Generate( bounds, clutter, scene );
	}

	/// <summary>
	/// Generates a hash from all serializable fields and properties using TypeLibrary.
	/// Override this if you need custom hash generation logic.
	/// </summary>
	public override int GetHashCode()
	{
		HashCode hash = new();
		var typeDesc = TypeLibrary.GetType( GetType() );

		if ( typeDesc == null )
			return base.GetHashCode();

		foreach ( var property in typeDesc.Properties )
		{
			// We only want [Property] stuff
			if ( !property.HasAttribute<PropertyAttribute>() )
				continue;

			var value = property.GetValue( this );
			hash.Add( value?.GetHashCode() ?? 0 );
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// Selects a random entry from the clutter based on weights.
	/// Returns null if no valid entries exist.
	/// </summary>
	protected ClutterEntry GetRandomEntry( ClutterDefinition clutter )
	{
		if ( clutter.IsEmpty )
			return null;

		float totalWeight = clutter.Entries.Sum( e => e.Weight );
		if ( totalWeight == 0 )
			return null;

		var randomValue = Random.Float( 0f, totalWeight );
		float currentWeight = 0f;

		foreach ( var entry in clutter.Entries )
		{
			if ( entry?.HasAsset != true || entry.Weight <= 0 )
				continue;

			currentWeight += entry.Weight;
			if ( randomValue <= currentWeight )
				return entry;
		}

		return null;
	}

	/// <summary>
	/// Helper to perform a ground trace at a position.
	/// Returns the trace result, or null if no scene is available.
	/// </summary>
	protected static SceneTraceResult? TraceGround( Scene scene, Vector3 position )
	{
		if ( scene == null )
			return null;

		// Unsure about this limit.... just needs to be high enough to not miss the scene
		var traceStart = new Vector3( position.x, position.y, 50000f );
		var traceEnd = new Vector3( position.x, position.y, -50000f );

		return scene.Trace
			.Ray( traceStart, traceEnd )
			.WithoutTags( "player", "trigger", "clutter" )
			.Run();
	}

	/// <summary>
	/// Generates a deterministic seed from tile coordinates and base seed.
	/// Use this to create unique seeds for different tiles.
	/// </summary>
	public static int GenerateSeed( int baseSeed, int x, int y )
	{
		int seed = baseSeed;
		seed = (seed * 397) ^ x;
		seed = (seed * 397) ^ y;
		return seed;
	}

}

public class SimpleScatterer : Scatterer
{
	[Property] public RangedFloat Scale { get; set; } = new RangedFloat( 0.8f, 1.2f );
	[Property] public int PointCount { get; set; } = 10;
	[Property, Group( "Placement" )] public bool PlaceOnGround { get; set; } = true;
	[Property, Group( "Placement" ), ShowIf( nameof( PlaceOnGround ), true )] public float HeightOffset { get; set; }
	[Property, Group( "Placement" ), ShowIf( nameof( PlaceOnGround ), true )] public bool AlignToNormal { get; set; }

	protected override List<ClutterInstance> Generate( BBox bounds, ClutterDefinition clutter, Scene scene = null )
	{
		scene ??= Game.ActiveScene;
		if ( scene == null || clutter == null )
			return [];

		var instances = new List<ClutterInstance>( PointCount );

		for ( int i = 0; i < PointCount; i++ )
		{
			var point = new Vector3(
				bounds.Mins.x + Random.Float( bounds.Size.x ),
				bounds.Mins.y + Random.Float( bounds.Size.y ),
				0f
			);

			var scale = Random.Float( Scale.Min, Scale.Max );
			var rotation = Rotation.FromYaw( Random.Float( 0f, 360f ) );

			if ( PlaceOnGround )
			{
				var trace = TraceGround( scene, point );
				if ( trace?.Hit != true )
					continue;

				point = trace.Value.HitPosition + trace.Value.Normal * HeightOffset;
				rotation = AlignToNormal
					? Rotation.From( new Angles( 0, Random.Float( 0f, 360f ), 0 ) ) * Rotation.FromToRotation( Vector3.Up, trace.Value.Normal )
					: Rotation.FromYaw( Random.Float( 0f, 360f ) );
			}

			var entry = GetRandomEntry( clutter );
			if ( entry == null )
				continue;

			instances.Add( new ClutterInstance
			{
				Transform = new Transform( point, rotation, scale ),
				Entry = entry
			} );
		}

		return instances;
	}
}
