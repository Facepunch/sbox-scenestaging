using System.Text.Json.Serialization;

namespace Sandbox.Clutter;

/// <summary>
/// A weighted collection of Prefabs and Models for random selection during clutter placement.
/// Think of it as a "palette" of variants that can be randomly selected.
/// </summary>
[AssetType( Name = "Clutter Definition", Extension = "clutter", Category = "Clutter" )]
public class ClutterDefinition : GameResource
{
	/// <summary>
	/// Tile size options for streaming mode.
	/// </summary>
	public enum TileSizeOption
	{
		[Title( "256" )] Size256 = 256,
		[Title( "512" )] Size512 = 512,
		[Title( "1024" )] Size1024 = 1024,
		[Title( "2048" )] Size2048 = 2048,
		[Title( "4096" )] Size4096 = 4096
	}

	/// <summary>
	/// List of weighted entries
	/// </summary>
	[Property]
	[Editor( "ClutterEntriesGrid" )]
	public List<ClutterEntry> Entries { get; set; } = [];

	public bool IsEmpty => Entries.Count == 0;

	/// <summary>
	/// Size of each tile in world units for infinite streaming mode.
	/// Smaller values = more frequent updates, larger values = better performance.
	/// </summary>
	[Property, Group( "Streaming" )]
	[Title( "Tile Size" )]
	public TileSizeOption TileSizeEnum { get; set; } = TileSizeOption.Size512;

	/// <summary>
	/// Gets the tile size as a float value.
	/// </summary>
	[Hide, JsonIgnore]
	public float TileSize => (float)TileSizeEnum;

	/// <summary>
	/// Number of tiles to generate around the camera in each direction.
	/// Higher values = more visible range but more memory usage.
	/// </summary>
	[Property, Group( "Streaming" ), Range( 1, 10 )]
	public int TileRadius { get; set; } = 4;

	private string _scattererTypeName;

	/// <summary>
	/// Type name of the scatterer to use (e.g., "SimpleScatterer", "SlopeScatterer").
	/// Change this to switch between different scatterer implementations.
	/// Available types will be shown when you click the property.
	/// </summary>
	[Property, Title( "Scatterer Type" ), Description( "Select the scatterer type from the dropdown" )]
	[Editor( "ScattererTypeSelector" )]
	public string ScattererTypeName
	{
		get => _scattererTypeName ?? nameof( SimpleScatterer );
		set
		{
			var normalizedValue = value ?? nameof( SimpleScatterer );

			if ( _scattererTypeName != normalizedValue )
			{
				_scattererTypeName = normalizedValue;
				
				// Always create a new scatterer when type changes
				_scatterer = CreateScatterer( normalizedValue );
				
				// Save the new scatterer's default data
				SaveScattererData();
			}
		}
	}

	/// <summary>
	/// JSON string storing the scatterer's property values.
	/// </summary>
	[Property, Hide]
	public string ScattererData { get; set; }

	private Scatterer _scatterer;

	/// <summary>
	/// The scatterer instance that defines how objects from this clutter definition are placed.
	/// Not serialized - created from ScattererTypeName and ScattererData on load.
	/// </summary>
	[JsonIgnore]
	[Editor( "ScattererSettings" )]
	[Property, Title( "Scatterer Settings" )]
	public Scatterer Scatterer
	{
		get
		{
			if ( _scatterer == null )
			{
				_scatterer = CreateScatterer( ScattererTypeName );
				ApplyScattererData();
			}
			return _scatterer;
		}
		set => _scatterer = value;
	}

	/// <summary>
	/// Applies stored ScattererData to the current scatterer instance.
	/// </summary>
	private void ApplyScattererData()
	{
		if ( _scatterer == null || string.IsNullOrEmpty( ScattererData ) )
			return;

		try
		{
			var loaded = Json.Deserialize( ScattererData, _scatterer.GetType() ) as Scatterer;
			if ( loaded != null )
			{
				_scatterer = loaded;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to apply scatterer data: {e.Message}" );
		}
	}

	/// <summary>
	/// Saves the current scatterer's properties to ScattererData.
	/// </summary>
	public void SaveScattererData()
	{
		if ( _scatterer != null )
		{
			ScattererData = Json.Serialize( _scatterer );
		}
	}

	protected override void PostLoad()
	{
		base.PostLoad();

		// Scatterer is lazily created when first accessed via the getter
		// Just reset the backing field so it gets recreated with fresh data
		_scatterer = null;
	}

	private static Scatterer CreateScatterer( string typeName )
	{
		if ( string.IsNullOrEmpty( typeName ) )
		{
			return new SimpleScatterer();
		}

		// Try to find the type by name
		var type = TypeLibrary.GetTypes()
			.FirstOrDefault( t => t.Name == typeName && t.TargetType?.IsAssignableTo( typeof( Scatterer ) ) == true );

		if ( type == null )
		{
			Log.Warning( $"Scatterer type '{typeName}' not found, using SimpleScatterer" );
			return new SimpleScatterer();
		}

		try
		{
			return TypeLibrary.Create<Scatterer>( type.TargetType );
		}
		catch ( Exception e )
		{
			Log.Error( e, $"Failed to create scatterer of type '{typeName}'. Using SimpleScatterer" );
			return new SimpleScatterer();
		}
	}

	/// <summary>
	/// Gets the number of valid entries (entries with assets and weight > 0).
	/// </summary>
	public int ValidEntryCount => Entries.Count( e => e is not null && e.HasAsset && e.Weight > 0 );

	/// <summary>
	/// Gets the sum of all valid entry weights.
	/// </summary>
	public float TotalWeight => Entries
		.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
		.Sum( e => e.Weight );

	/// <summary>
	/// Selects a random entry based on weights using Game.Random.
	/// Returns null if no valid entries exist.
	/// </summary>
	public ClutterEntry GetRandomEntry()
	{
		var validEntries = Entries
			.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
			.ToList();

		if ( validEntries.Count == 0 )
			return null;

		var totalWeight = validEntries.Sum( e => e.Weight );
		var randomValue = Game.Random.Float( 0f, totalWeight );

		float cumulativeWeight = 0f;
		foreach ( var entry in validEntries )
		{
			cumulativeWeight += entry.Weight;
			if ( randomValue <= cumulativeWeight )
			{
				return entry;
			}
		}

		// Fallback to last entry
		return validEntries[^1];
	}

	/// <summary>
	/// Validates the clutter definition configuration and logs warnings if issues are found.
	/// </summary>
	public void Validate()
	{
		if ( Entries == null || Entries.Count == 0 )
		{
			Log.Warning( $"ClutterDefinition '{ResourceName}': No entries defined" );
			return;
		}

		if ( ValidEntryCount == 0 )
		{
			Log.Warning( $"ClutterDefinition '{ResourceName}': No valid entries (all weights are 0 or no assets assigned)" );
			return;
		}

		var invalidCount = Entries.Count - ValidEntryCount;
		if ( invalidCount > 0 )
		{
			Log.Info( $"ClutterDefinition '{ResourceName}': {invalidCount} invalid entries (missing assets or zero weight)" );
		}
	}

	/// <summary>
	/// Generates a hash from all clutter definition properties including entries and scatterer settings.
	/// </summary>
	public override int GetHashCode()
	{
		var hash = new HashCode();

		// Hash streaming settings
		hash.Add( TileSize );
		hash.Add( TileRadius );

		// Hash entry count and each entry's properties
		hash.Add( Entries?.Count ?? 0 );
		if ( Entries != null )
		{
			foreach ( var entry in Entries )
			{
				if ( entry != null )
				{
					hash.Add( entry.Weight );
					hash.Add( entry.Model?.GetHashCode() ?? 0 );
					hash.Add( entry.Prefab?.GetHashCode() ?? 0 );
				}
			}
		}

		// Hash scatterer type and settings
		hash.Add( ScattererTypeName?.GetHashCode() ?? 0 );
		hash.Add( Scatterer?.GetHashCode() ?? 0 );

		return hash.ToHashCode();
	}
}
