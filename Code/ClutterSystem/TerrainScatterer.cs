using System.Text.Json.Serialization;

namespace Sandbox.Clutter;

/// <summary>
/// Maps an clutter entry to a slope angle range.
/// </summary>
public class SlopeMapping
{
	[Property, Range( 0, 90 )]
	[Description( "Minimum slope angle (degrees) for this entry" )]
	public float MinAngle { get; set; } = 0f;

	[Property, Range( 0, 90 )]
	[Description( "Maximum slope angle (degrees) for this entry" )]
	public float MaxAngle { get; set; } = 45f;

	[Property]
	[Title( "Entry" )]
	[Editor( "ClutterEntryPicker" )]
	[Description( "Which clutter entry to use for this slope range" )]
	public int EntryIndex { get; set; } = 0;

	public override int GetHashCode() => HashCode.Combine( MinAngle, MaxAngle, EntryIndex );
}

/// <summary>
/// Scatterer that filters and selects assets based on the slope angle of the surface.
/// Useful for placing different vegetation or rocks on flat vs steep terrain.
/// </summary>
public class SlopeScatterer : Scatterer
{
	[Property]
	[Description( "Scale range for spawned objects" )]
	public RangedFloat Scale { get; set; } = new RangedFloat( 0.8f, 1.2f );

	[Property, Range( 0.001f, 10f )]
	[Description( "Points per square meter (density)" )]
	public float Density { get; set; } = 0.1f;

	[Property, Group( "Placement" )]
	[Description( "Offset from ground surface" )]
	public float HeightOffset { get; set; } = 0f;

	[Property, Group( "Placement" )]
	[Description( "Align objects to surface normal" )]
	public bool AlignToNormal { get; set; } = false;

	[Property, Group( "Slope Mappings" )]
	[Description( "Define which entries spawn at which slope angles" )]
	public List<SlopeMapping> Mappings { get; set; } = new();

	[Property]
	[Description( "Use random clutter entry if no slope mapping matches" )]
	public bool UseFallback { get; set; } = true;

	protected override List<ClutterInstance> Generate( BBox bounds, ClutterDefinition clutter, Scene scene = null )
	{
		scene ??= Game.ActiveScene;
		if ( scene == null || clutter == null || clutter.IsEmpty )
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

			// Trace to ground
			var trace = TraceGround( scene, point );
			if ( trace?.Hit != true )
				continue;

			// Calculate slope angle
			var normal = trace.Value.Normal;
			var slopeAngle = Vector3.GetAngle( Vector3.Up, normal );

		var entry = GetEntryForSlope( clutter, slopeAngle );
		if ( entry == null )
		{
			if ( UseFallback )
			{
				entry = GetRandomEntry( clutter );
			}
			if ( entry == null )
				continue;
		}

			// Setup transform
			var scale = Random.Float( Scale.Min, Scale.Max );
			var yaw = Random.Float( 0f, 360f );
			var rotation = AlignToNormal
				? GetAlignedRotation( normal, yaw )
				: Rotation.FromYaw( yaw );

			var position = trace.Value.HitPosition + normal * HeightOffset;

			instances.Add( new ClutterInstance
			{
				Transform = new Transform( position, rotation, scale ),
				Entry = entry
			} );
		}

		return instances;
	}

	/// <summary>
	/// Finds an entry that matches the given slope angle based on mappings.
	/// </summary>
	private ClutterEntry GetEntryForSlope( ClutterDefinition clutter, float slopeAngle )
	{
		if ( Mappings is null or { Count: 0 } )
			return GetRandomEntry( clutter );

		var matchCount = 0;
		foreach ( var m in Mappings )
		{
			if ( slopeAngle >= m.MinAngle && slopeAngle <= m.MaxAngle )
				matchCount++;
		}

		if ( matchCount is 0 )
			return null;

		// Pick a random index within matching mappings
		var randomIndex = Random.Int( 0, matchCount - 1 );
		var currentIndex = 0;

		foreach ( var m in Mappings )
		{
			if ( slopeAngle >= m.MinAngle && slopeAngle <= m.MaxAngle )
			{
				if ( currentIndex == randomIndex )
				{
					if ( m.EntryIndex >= 0 && m.EntryIndex < clutter.Entries.Count )
					{
						var entry = clutter.Entries[m.EntryIndex];
						if ( entry?.HasAsset is true )
							return entry;
					}
					break;
				}
				currentIndex++;
			}
		}

		return null;
	}
}

/// <summary>
/// Maps a terrain material to a list of clutter entries that can spawn on it.
/// </summary>
public class TerrainMaterialMapping
{
	[Property]
	[Description( "The terrain material to match" )]
	public TerrainMaterial Material { get; set; }

	[Property]
	[Title( "Entry Indices" )]
	[Description( "Indices of clutter entries that can spawn on this material" )]
	public List<int> EntryIndices { get; set; } = [];

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add( Material?.GetHashCode() ?? 0 );
		foreach ( var index in EntryIndices )
			hash.Add( index );
		return hash.ToHashCode();
	}
}

/// <summary>
/// Scatterer that selects assets based on the terrain material at the hit position.
/// Useful for placing different vegetation on different terrain textures (grass, dirt, rock, etc).
/// </summary>
public class TerrainMaterialScatterer : Scatterer
{
	[Property]
	[Description( "Scale range for spawned objects" )]
	public RangedFloat Scale { get; set; } = new RangedFloat( 0.8f, 1.2f );

	[Property, Range( 0.001f, 10f )]
	[Description( "Points per square meter (density)" )]
	public float Density { get; set; } = 0.1f;

	[Property, Group( "Placement" )]
	[Description( "Offset from ground surface" )]
	public float HeightOffset { get; set; } = 0f;

	[Property, Group( "Placement" )]
	[Description( "Align objects to surface normal" )]
	public bool AlignToNormal { get; set; } = false;

	[Property, Group( "Placement" )]
	[Description( "Apply random rotation around vertical axis" )]
	public bool RandomYaw { get; set; } = true;

	[Property, Group( "Material Mappings" )]
	[Description( "Define which entries spawn on which terrain materials" )]
	public List<TerrainMaterialMapping> Mappings { get; set; } = new();

	[Property, Group( "Fallback" )]
	[Description( "Use random clutter entry if no material mapping matches or no terrain is present" )]
	public bool UseFallback { get; set; } = true;

	/// <summary>
	/// Cached terrain reference to avoid repeated GetComponent calls within same tile.
	/// </summary>
	[JsonIgnore, Hide]
	private Terrain _cachedTerrain;

	[JsonIgnore, Hide]
	private GameObject _cachedTerrainObject;

	protected override List<ClutterInstance> Generate( BBox bounds, ClutterDefinition clutter, Scene scene = null )
	{
		scene ??= Game.ActiveScene;
		if ( scene == null || clutter == null || clutter.IsEmpty )
			return [];

		// Clear terrain cache for new generation
		_cachedTerrain = null;
		_cachedTerrainObject = null;

		var pointCount = CalculatePointCount( bounds, Density );
		var instances = new List<ClutterInstance>( pointCount );

		for ( int i = 0; i < pointCount; i++ )
		{
			var point = new Vector3(
				bounds.Mins.x + Random.Float( bounds.Size.x ),
				bounds.Mins.y + Random.Float( bounds.Size.y ),
				0f
			);

			// Trace to ground
			var trace = TraceGround( scene, point );
			if ( trace?.Hit != true )
				continue;

		var terrain = GetTerrainFromTrace( trace.Value );
		if ( terrain == null )
		{
			if ( UseFallback )
			{
				var fallbackEntry = GetRandomEntry( clutter );
				if ( fallbackEntry != null )
				{
					instances.Add( CreateInstance( trace.Value, fallbackEntry ) );
				}
			}
			continue;
		}

			// Query terrain material at hit position
			var materialInfo = terrain.GetMaterialAtWorldPosition( trace.Value.HitPosition );
			if ( !materialInfo.HasValue || materialInfo.Value.IsHole )
				continue;

			// Find matching entry from material mappings
			var entry = GetEntryForMaterial( clutter, materialInfo.Value );
			if ( entry == null )
			{
				if ( UseFallback )
				{
					entry = GetRandomEntry( clutter );
				}
				if ( entry == null )
					continue;
			}

			instances.Add( CreateInstance( trace.Value, entry ) );
		}

		return instances;
	}

	private ClutterInstance CreateInstance( SceneTraceResult trace, ClutterEntry entry )
	{
		var scale = Random.Float( Scale.Min, Scale.Max );
		var normal = trace.Normal;
		var yaw = RandomYaw ? Random.Float( 0f, 360f ) : 0f;
		
		Rotation rotation;
		if ( AlignToNormal )
		{
			rotation = GetAlignedRotation( normal, yaw );
		}
		else
		{
			rotation = Rotation.FromYaw( yaw );
		}

		var position = trace.HitPosition + normal * HeightOffset;

		return new ClutterInstance
		{
			Transform = new Transform( position, rotation, scale ),
			Entry = entry
		};
	}

	/// <summary>
	/// Gets the Terrain component from a trace result, with caching.
	/// </summary>
	private Terrain GetTerrainFromTrace( SceneTraceResult trace )
	{
		var hitObject = trace.GameObject;
		if ( hitObject == null )
			return null;

		// Use cached terrain if hitting same object
		if ( _cachedTerrainObject == hitObject )
			return _cachedTerrain;

		// Cache the terrain lookup
		_cachedTerrainObject = hitObject;
		_cachedTerrain = hitObject.Components.Get<Terrain>();

		return _cachedTerrain;
	}

	/// <summary>
	/// Finds an entry that matches the terrain material at the given position.
	/// </summary>
	private ClutterEntry GetEntryForMaterial( ClutterDefinition clutter, Terrain.TerrainMaterialInfo materialInfo )
	{
		if ( Mappings is null or { Count: 0 } )
			return null;

		// Get the dominant material
		var dominantMaterial = materialInfo.GetDominantMaterial();
		if ( dominantMaterial is null )
			return null;

		// Find mapping for this material
		var mapping = Mappings.FirstOrDefault( m => m.Material == dominantMaterial );
		if ( mapping is null || mapping.EntryIndices is null or { Count: 0 } )
			return null;

		var totalWeight = 0f;
		foreach ( var index in mapping.EntryIndices )
		{
			if ( index >= 0 && index < clutter.Entries.Count )
			{
				var entry = clutter.Entries[index];
				if ( entry?.HasAsset is true && entry.Weight > 0 )
					totalWeight += entry.Weight;
			}
		}

		if ( totalWeight <= 0 )
			return null;

		// Pick a weighted random entry
		var randomValue = Random.Float( 0f, totalWeight );
		var currentWeight = 0f;

		foreach ( var index in mapping.EntryIndices )
		{
			if ( index >= 0 && index < clutter.Entries.Count )
			{
				var entry = clutter.Entries[index];
				if ( entry?.HasAsset is true && entry.Weight > 0 )
				{
					currentWeight += entry.Weight;
					if ( randomValue <= currentWeight )
						return entry;
				}
			}
		}

		// Fallback: return last valid entry
		for ( var i = mapping.EntryIndices.Count - 1; i >= 0; i-- )
		{
			var index = mapping.EntryIndices[i];
			if ( index >= 0 && index < clutter.Entries.Count )
			{
				var entry = clutter.Entries[index];
				if ( entry?.HasAsset is true && entry.Weight > 0 )
					return entry;
			}
		}

		return null;
	}
}
