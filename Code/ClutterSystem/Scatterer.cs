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
public abstract class Scatterer
{
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

		// Include type name in hash
		hash.Add( GetType().Name );

		foreach ( var property in typeDesc.Properties )
		{
			if ( !property.HasAttribute<PropertyAttribute>() )
				continue;

			var value = property.GetValue( this );
			HashValue( ref hash, value );
		}

		return hash.ToHashCode();
	}

	/// <summary>
	/// Hashes a value, handling collections properly.
	/// </summary>
	private static void HashValue( ref HashCode hash, object value )
	{
		if ( value == null )
		{
			hash.Add( 0 );
			return;
		}

		// Handle collections by hashing their contents
		if ( value is System.Collections.IEnumerable enumerable && value is not string )
		{
			foreach ( var item in enumerable )
			{
				HashValue( ref hash, item );
			}
			return;
		}

		hash.Add( value.GetHashCode() );
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
	/// Creates a rotation aligned to a surface normal with random yaw.
	/// </summary>
	protected Rotation GetAlignedRotation( Vector3 normal, float yawDegrees )
	{
		// First align to the surface normal
		var alignToSurface = Rotation.FromToRotation( Vector3.Up, normal );
		// Then apply yaw rotation around the new up axis (the normal)
		var yawRotation = Rotation.FromAxis( normal, yawDegrees );
		return yawRotation * alignToSurface;
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

	/// <summary>
	/// Calculates the number of points to scatter based on density and area.
	/// Caps at maxPoints to prevent engine freezing.
	/// </summary>
	/// <param name="bounds">Bounds to scatter in</param>
	/// <param name="density">Points per square meter</param>
	/// <param name="maxPoints">Maximum points to cap at (default 10000)</param>
	/// <returns>Number of points to generate</returns>
	protected int CalculatePointCount( BBox bounds, float density, int maxPoints = 10000 )
	{
		const float PointsPerUnit = 512f;
		var area = (bounds.Size.x / PointsPerUnit) * ( bounds.Size.y / PointsPerUnit);
		var desiredCount = area * density;
		
		// Handle fractional points probabilistically
		// e.g., 1.3 points = 1 guaranteed + 30% chance of 1 more
		var guaranteedPoints = (int)desiredCount;
		var fractionalPart = desiredCount - guaranteedPoints;
		
		var finalCount = guaranteedPoints;
		if ( Random.Float( 0f, 1f ) < fractionalPart )
		{
			finalCount++;
		}
		
		var clampedCount = Math.Clamp( finalCount, 0, maxPoints );
		
		if ( desiredCount > maxPoints )
		{
			Log.Warning( $"Scatterer: Density would generate {desiredCount} points, capped to {maxPoints} to prevent freezing. " +
			            $"Area: {area:F1}m�, Density: {density:F3}/m�. Consider reducing density or using smaller bounds." );
		}
		
		return clampedCount;
	}
}

public class SimpleScatterer : Scatterer
{
	[Property] 
	[Description( "Scale range for spawned objects" )]
	public RangedFloat Scale { get; set; } = new RangedFloat( 0.8f, 1.2f );
	
	[Property, Range( 0.001f, 10f )]
	[Description( "Points per square meter (density)" )]
	public float Density { get; set; } = 0.1f;
	
	[Property, Group( "Placement" )] 
	public bool PlaceOnGround { get; set; } = true;
	
	[Property, Group( "Placement" ), ShowIf( nameof( PlaceOnGround ), true )] 
	public float HeightOffset { get; set; }
	
	[Property, Group( "Placement" ), ShowIf( nameof( PlaceOnGround ), true )] 
	public bool AlignToNormal { get; set; }

	protected override List<ClutterInstance> Generate( BBox bounds, ClutterDefinition clutter, Scene scene = null )
	{
		scene ??= Game.ActiveScene;
		if ( scene == null || clutter == null )
			return [];

		var pointCount = CalculatePointCount( bounds, Density );
		var instances = new List<ClutterInstance>( pointCount );

		for ( int i = 0; i < pointCount; i++ )
		{
			var point = new Vector3(
				bounds.Mins.x + Random.Float( bounds.Size.x ),
				bounds.Mins.y + Random.Float( bounds.Size.y ),
				0f
			);

			var scale = Random.Float( Scale.Min, Scale.Max );
			var yaw = Random.Float( 0f, 360f );
			var rotation = Rotation.FromYaw( yaw );

			if ( PlaceOnGround )
			{
				var trace = TraceGround( scene, point );
				if ( trace?.Hit != true )
					continue;

				point = trace.Value.HitPosition + trace.Value.Normal * HeightOffset;
				rotation = AlignToNormal
					? GetAlignedRotation( trace.Value.Normal, yaw )
					: Rotation.FromYaw( yaw );
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
