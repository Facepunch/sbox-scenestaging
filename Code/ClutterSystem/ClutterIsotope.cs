using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sandbox;

/// <summary>
/// A weighted collection of Prefabs and Models for random selection during clutter placement.
/// Think of it as a "palette" of variants that can be randomly selected.
/// </summary>
[GameResource( "Clutter Isotope", "isotope", "A weighted collection of objects for clutter scattering", Icon = "grass" )]
public class ClutterIsotope : GameResource
{
	/// <summary>
	/// List of weighted entries (Prefabs or Models with weights).
	/// </summary>
	[Property]
	public List<IsotopeEntry> Entries { get; set; } = new();

	/// <summary>
	/// Type name of the scatterer to use (e.g., "SimpleScatterer", "PoissonScatterer").
	/// Change this to switch between different scatterer implementations.
	/// Available types will be shown when you click the property.
	/// </summary>
	[Property, Title( "Scatterer Type" ), Description( "Select the scatterer type from the dropdown" )]
	[Editor( "ScattererTypeSelector" )]
	public string ScattererTypeName
	{
		get => _scattererTypeName;
		set
		{
			if ( _scattererTypeName != value )
			{
				_scattererTypeName = value;
				// Recreate scatterer when type changes
				Scatterer = CreateScatterer( value ?? nameof(SimpleScatterer) );
			}
		}
	}
	private string _scattererTypeName = nameof(SimpleScatterer);

	/// <summary>
	/// The scatterer instance that defines how objects from this isotope are placed.
	/// Automatically recreated when ScattererTypeName changes.
	/// </summary>
	[Property, Title( "Scatterer Settings" )]
	[Editor( "ScattererSettings" )]
	public Scatterer Scatterer { get; set; } = new SimpleScatterer();

	private static Scatterer CreateScatterer( string typeName )
	{
		if ( string.IsNullOrEmpty( typeName ) )
		{
			return new SimpleScatterer();
		}

		// Try to find the type by name
		var type = TypeLibrary.GetTypes()
			.FirstOrDefault( t => t.Name == typeName && t.TargetType?.IsAssignableTo( typeof(Scatterer) ) == true );

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
	public IsotopeEntry GetRandomEntry()
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
	/// Validates the isotope configuration and logs warnings if issues are found.
	/// </summary>
	public void Validate()
	{
		if ( Entries == null || Entries.Count == 0 )
		{
			Log.Warning( $"Isotope '{ResourceName}': No entries defined" );
			return;
		}

		if ( ValidEntryCount == 0 )
		{
			Log.Warning( $"Isotope '{ResourceName}': No valid entries (all weights are 0 or no assets assigned)" );
			return;
		}

		var invalidCount = Entries.Count - ValidEntryCount;
		if ( invalidCount > 0 )
		{
			Log.Info( $"Isotope '{ResourceName}': {invalidCount} invalid entries (missing assets or zero weight)" );
		}
	}

	protected override void PostLoad()
	{
		base.PostLoad();
		
		// If no scatterer or type name changed, recreate it
		if ( Scatterer == null || Scatterer.GetType().Name != ScattererTypeName )
		{
			Scatterer = CreateScatterer( ScattererTypeName ?? nameof(SimpleScatterer) );
		}
	}
}
