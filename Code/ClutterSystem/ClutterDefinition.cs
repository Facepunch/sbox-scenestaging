using System.Text.Json.Serialization;

namespace Sandbox.Clutter;

/// <summary>
/// A weighted collection of Prefabs and Models for random selection during clutter placement.
/// Think of it as a "palette" of variants that can be randomly selected.
/// </summary>
[Icon( "grass" )]
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
	[Property]
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
	[Property, Range( 1, 10 )]
	public int TileRadius { get; set; } = 4;

	/// <summary>
	/// Scatterer Type
	/// </summary>
	[Property]
	[Title( "Scatterer Type" ), Description( "Select the scatterer type from the dropdown" )]
	[Editor( "ScattererTypeSelector" )]
	public string ScattererTypeName
	{
		get => field;
		set
		{
			field = value;
			Scatterer = CreateScattererInstance( value );
		}
	} = nameof( SimpleScatterer );

	/// <summary>
	/// The scatterer instance
	/// </summary>
	[Property]
	public Scatterer Scatterer { get; set; } = new SimpleScatterer();


	/// <summary>
	/// Creates a new scatterer instance based on the type name.
	/// </summary>
	private static Scatterer CreateScattererInstance( string typeName )
	{
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
			Log.Error( e, $"Failed to create scatterer of type '{typeName}'" );
			return new SimpleScatterer();
		}
	}

	/// <summary>
	/// Called after the resource is loaded from JSON.
	/// Ensures the scatterer instance matches the scatterer type name.
	/// </summary>
	protected override void PostLoad()
	{
		base.PostLoad();

		if ( Scatterer is null )
			Scatterer = CreateScattererInstance( ScattererTypeName );
	}

	/// <summary>
	/// Generates a hash from all clutter definition properties including entries and scatterer settings.
	/// </summary>
	public override int GetHashCode()
	{
		var hash = new HashCode();

		hash.Add( TileSize );
		hash.Add( TileRadius );
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

		hash.Add( ScattererTypeName?.GetHashCode() ?? 0 );
		hash.Add( Scatterer?.GetHashCode() ?? 0 );

		return hash.ToHashCode();
	}
}

