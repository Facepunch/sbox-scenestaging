using System.Text.Json.Nodes;

namespace Sandbox;

/// <summary>
/// This is what is given to custom scatter functions.
/// </summary>
public record class ScatterContext
{
	public SceneTraceResult HitTest { get; init; }
	public ClutterLayer Layer { get; init; }
	public ClutterResources Resources { get; init; }
	public float Density { get; init; }

	// Surface info
	public Vector3 Position => HitTest.HitPosition;
	public Vector3 Normal => HitTest.Normal;
	public float SlopeAngle => Normal.Angle( Vector3.Up );

	public bool CanScatter => Layer.HasObjects;
	public ClutterObject RandomObject => Layer.GetRandomObject();

	// Convenience methods
	public ClutterInstance? CreateInstance( ClutterObject obj, Transform transform )
		=> Resources.CreateInstance( obj, transform );

	public Transform CreateTransform( Vector3? position = null, Rotation? rotation = null, float scale = 1f )
		=> new( position ?? Position, rotation ?? Rotation.Identity, scale );
}

public class ScattererBase
{
	public virtual ClutterInstance? Scatter( ScatterContext context ) { return null; }

	/// <summary>
	/// Default implementation generates random points within bounds based on density.
	/// Override this method to provide custom point distribution patterns.
	/// </summary>
	public virtual IEnumerable<Vector3> GeneratePoints( BBox bounds, float density )
	{
		// Calculate number of points based on area and density
		var area = bounds.Size.x * bounds.Size.y;
		var pointCount = (int)(area * density / 10000f); // Adjust divisor for desired spacing

		var points = new List<Vector3>();
		for ( int i = 0; i < pointCount; i++ )
		{
			var point = new Vector3(
				Game.Random.Float( bounds.Mins.x, bounds.Maxs.x ),
				Game.Random.Float( bounds.Mins.y, bounds.Maxs.y ),
				bounds.Center.z
			);
			points.Add( point );
		}

		return points;
	}

	public virtual JsonObject Serialize()
	{
		var settings = new JsonObject();
		var typeDesc = TypeLibrary.GetType( GetType() );

		// Serialize all properties marked with [Property]
		foreach ( var prop in typeDesc.Properties )
		{
			if ( !prop.HasAttribute<PropertyAttribute>() )
				continue;

			var value = prop.GetValue( this );
			settings[prop.Name] = Json.ToNode( value, prop.PropertyType );
		}

		return settings;
	}

	public virtual void Deserialize( JsonObject settings )
	{
		if ( settings == null )
			return;

		var typeDesc = TypeLibrary.GetType( GetType() );

		// Deserialize all properties marked with [Property]
		foreach ( var prop in typeDesc.Properties )
		{
			if ( !prop.HasAttribute<PropertyAttribute>() )
				continue;

			if ( !settings.TryGetPropertyValue( prop.Name, out var node ) )
				continue;

			var value = Json.FromNode( node, prop.PropertyType );
			prop.SetValue( this, value );
		}
	}
}

/// <summary>
/// Basic scatter that picks a random object from the layer. Can align to surface and have a slope threshold as well as a random scale.
/// </summary>
public class DefaultScatterer : ScattererBase
{
	[Property] public float MinScale { get; set; } = 0.8f;
	[Property] public float MaxScale { get; set; } = 1.2f;
	[Property] public bool AlignToSurface { get; set; } = true;
	[Property] public float MaxSlope { get; set; } = 45f;

	public override string ToString() => "Random Scatter";

	public override ClutterInstance? Scatter( ScatterContext context )
	{
		if ( Game.Random.Float( 0f, 1f ) > context.Density )
			return null;

		if ( context.SlopeAngle > MaxSlope )
			return null;

		if ( !context.CanScatter )
			return null;

		var transform = context.CreateTransform( scale: Game.Random.Float( MinScale, MaxScale ) );

		if ( AlignToSurface )
		{
			transform.Rotation.SlerpTo( Rotation.LookAt( Vector3.Forward, context.Normal ), 0.5f );
		}

		return context.CreateInstance( context.RandomObject, transform );
	}
}

/// <summary>
/// Poisson disc pattern scatterer keeping a uniform distance between each point
/// </summary>
public class PoissonDiskScatterer : ScattererBase
{
	[Property] public float MinDistance { get; set; } = 50f;
	[Property] public int MaxAttempts { get; set; } = 30;
	[Property] public float MinScale { get; set; } = 0.8f;
	[Property] public float MaxScale { get; set; } = 1.2f;
	[Property] public bool AlignToSurface { get; set; } = false;
	[Property] public ParticleFloat ZRotation { get; set; } = new ParticleFloat( 0f, 360f );

	public override string ToString() => "Poisson Disk Distribution";

	public override IEnumerable<Vector3> GeneratePoints( BBox bounds, float density )
	{
		var points = new List<Vector3>();
		var activeList = new List<Vector3>();
		var cellSize = MinDistance / MathF.Sqrt( 2 );
		var gridWidth = (int)MathF.Ceiling( bounds.Size.x / cellSize );
		var gridHeight = (int)MathF.Ceiling( bounds.Size.y / cellSize );
		var grid = new Vector3?[gridWidth, gridHeight];

		// Start with initial random point (scatter in XY plane, Z is up so Z is fixed)
		var firstPoint = new Vector3(
			Game.Random.Float( bounds.Mins.x, bounds.Maxs.x ),
			Game.Random.Float( bounds.Mins.y, bounds.Maxs.y ),
			bounds.Center.z
		);

		points.Add( firstPoint );
		activeList.Add( firstPoint );

		var gridX = (int)((firstPoint.x - bounds.Mins.x) / cellSize);
		var gridY = (int)((firstPoint.y - bounds.Mins.y) / cellSize);
		if ( gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight )
			grid[gridX, gridY] = firstPoint;

		while ( activeList.Count > 0 )
		{
			var randomIndex = Game.Random.Int( 0, activeList.Count - 1 );
			var point = activeList[randomIndex];
			bool found = false;

			for ( int i = 0; i < MaxAttempts; i++ )
			{
				var angle = Game.Random.Float( 0, 2 * MathF.PI );
				var radius = Game.Random.Float( MinDistance, 2 * MinDistance );
				var candidate = point + new Vector3(
					MathF.Cos( angle ) * radius,
					MathF.Sin( angle ) * radius,
					0
				);

				if ( IsValidPoint( candidate, bounds, grid, cellSize, gridWidth, gridHeight, bounds.Mins ) )
				{
					points.Add( candidate );
					activeList.Add( candidate );

					gridX = (int)((candidate.x - bounds.Mins.x) / cellSize);
					gridY = (int)((candidate.y - bounds.Mins.y) / cellSize);
					if ( gridX >= 0 && gridX < gridWidth && gridY >= 0 && gridY < gridHeight )
						grid[gridX, gridY] = candidate;

					found = true;
					break;
				}
			}

			if ( !found )
				activeList.RemoveAt( randomIndex );
		}

		return points;
	}

	private bool IsValidPoint( Vector3 candidate, BBox bounds, Vector3?[,] grid, float cellSize, int gridWidth, int gridHeight, Vector3 boundsMin )
	{
		if ( candidate.x < bounds.Mins.x || candidate.x > bounds.Maxs.x ||
			 candidate.y < bounds.Mins.y || candidate.y > bounds.Maxs.y )
			return false;

		var gridX = (int)((candidate.x - boundsMin.x) / cellSize);
		var gridY = (int)((candidate.y - boundsMin.y) / cellSize);

		for ( int x = (int)MathF.Max( 0, gridX - 2 ); x < (int)MathF.Min( gridWidth, gridX + 3 ); x++ )
		{
			for ( int y = (int)MathF.Max( 0, gridY - 2 ); y < (int)MathF.Min( gridHeight, gridY + 3 ); y++ )
			{
				if ( grid[x, y].HasValue )
				{
					var distance = Vector3.DistanceBetween( candidate, grid[x, y].Value );
					if ( distance < MinDistance )
						return false;
				}
			}
		}

		return true;
	}

	public override ClutterInstance? Scatter( ScatterContext context )
	{
		// Use density to randomly skip some spawn points
		if ( Game.Random.Float( 0f, 1f ) > context.Density )
			return null;

		// Simple scatter - just place object at hit position
		if ( !context.CanScatter )
			return null;

		var rotation = Rotation.Identity;

		// Align to surface normal if enabled (Z is up)
		if ( AlignToSurface )
		{
			// Create rotation that aligns Z-axis with surface normal
			rotation = Rotation.LookAt( Vector3.Forward, context.Normal );
		}

		// Apply Z rotation
		var zRotation = ZRotation.Evaluate( Game.Random.Float( 0f, 1f ), Game.Random.Float( 0f, 1f ) );
		rotation *= Rotation.FromAxis( Vector3.Up, zRotation );

		var transform = context.CreateTransform( rotation: rotation, scale: Game.Random.Float( MinScale, MaxScale ) );

		// Load and instantiate the object
		return context.CreateInstance( context.RandomObject, transform );
	}
}


/// <summary>
/// A more advanced scatterer which allows to use elevation and slop level as control points. it can transition between layers depending on slope angle.
/// </summary>
public class SlopeBasedScatterer : ScattererBase
{
	[Property, Title( "Slope Angle Threshold" )]
	public float SlopeAngle { get; set; } = 30f;

	[Property, Title( "Steep Slope Layer" )]
	public ClutterLayer SteepSlopeLayer { get; set; }

	[Property, Title( "Gentle Slope Layer" )]
	public ClutterLayer GentleSlopeLayer { get; set; }

	[Property]
	public bool AlignToSurface { get; set; } = true;

	[Property]
	public float MinScale { get; set; } = 0.8f;

	[Property]
	public float MaxScale { get; set; } = 1.2f;

	public override string ToString() => "Slope & Altitude Based";

	[Property, Title( "Density Over Slope" ), Group( "Curves" )]
	public ParticleFloat DensityOverSlope { get; set; } = 1.0f;

	[Property, Title( "Scale Over Slope" ), Group( "Curves" )]
	public ParticleFloat ScaleOverSlope { get; set; } = 1.0f;

	[Property, Title( "Z Rotation" ), Group( "Curves" )]
	public ParticleFloat ZRotation { get; set; } = new ParticleFloat( 0f, 360f );

	[Property, Title( "Use Altitude" ), Group( "Altitude" )]
	public bool UseAltitude { get; set; } = false;

	[Property, Title( "Min Altitude" ), Group( "Altitude" ), ShowIf( "UseAltitude", true )]
	public float MinAltitude { get; set; } = 0f;

	[Property, Title( "Max Altitude" ), Group( "Altitude" ), ShowIf( "UseAltitude", true )]
	public float MaxAltitude { get; set; } = 1000f;

	[Property, Title( "Density Over Altitude" ), Group( "Altitude" ), ShowIf( "UseAltitude", true )]
	public ParticleFloat DensityOverAltitude { get; set; } = 1.0f;

	[Property, Title( "Scale Over Altitude" ), Group( "Altitude" ), ShowIf( "UseAltitude", true )]
	public ParticleFloat ScaleOverAltitude { get; set; } = 1.0f;

	public override ClutterInstance? Scatter( ScatterContext context )
	{
		// Calculate slope angle from surface normal (Z is up)
		var normalizedAngle = context.SlopeAngle / 90f;

		// Apply density over slope curve
		var slopeDensity = DensityOverSlope.Evaluate( normalizedAngle, Game.Random.Float( 0f, 1f ) );
		var finalDensity = context.Density * slopeDensity;

		// Apply altitude-based density if enabled
		if ( UseAltitude )
		{
			var altitude = context.Position.z;
			var normalizedAltitude = MathX.Remap( altitude, MinAltitude, MaxAltitude, 0f, 1f, true );
			var altitudeDensity = DensityOverAltitude.Evaluate( normalizedAltitude, Game.Random.Float( 0f, 1f ) );
			finalDensity *= altitudeDensity;
		}

		// Use final density to randomly skip spawn points
		if ( Game.Random.Float( 0f, 1f ) > finalDensity )
			return null;

		// Determine which layer to use based on slope
		var targetLayer = context.SlopeAngle > SlopeAngle ? SteepSlopeLayer : GentleSlopeLayer;

		// Only scatter if current layer matches the target layer
		if ( targetLayer == null || context.Layer != targetLayer )
			return null;

		// Get random object from the matched layer
		if ( !context.CanScatter )
			return null;

		var rotation = Rotation.Identity;
		if ( AlignToSurface )
		{
			rotation = Rotation.LookAt( Vector3.Forward, context.Normal );
		}

		var zRotation = ZRotation.Evaluate( Game.Random.Float( 0f, 1f ), Game.Random.Float( 0f, 1f ) );
		rotation *= Rotation.FromAxis( Vector3.Up, zRotation );

		// Calculate base scale
		var baseScale = Game.Random.Float( MinScale, MaxScale );

		// Apply scale over slope curve
		var slopeScaleMultiplier = ScaleOverSlope.Evaluate( normalizedAngle, Game.Random.Float( 0f, 1f ) );
		var scale = baseScale * slopeScaleMultiplier;

		if ( UseAltitude )
		{
			var altitude = context.Position.z;
			var normalizedAltitude = MathX.Remap( altitude, MinAltitude, MaxAltitude, 0f, 1f, true );
			var altitudeScaleMultiplier = ScaleOverAltitude.Evaluate( normalizedAltitude, Game.Random.Float( 0f, 1f ) );
			scale *= altitudeScaleMultiplier;
		}

		var transform = context.CreateTransform( rotation: rotation, scale: scale );

		// Load and instantiate the object
		return context.CreateInstance( context.RandomObject, transform );
	}
}
