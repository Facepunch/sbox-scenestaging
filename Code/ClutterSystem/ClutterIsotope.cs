using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sandbox;

/// <summary>
/// A weighted collection of Prefabs and Models for procedural clutter placement.
/// Isotopes allow you to define variants of similar objects (rocks, grass, debris) 
/// with different spawn probabilities based on weight.
/// </summary>
[GameResource( "Clutter Isotope", "isotope", "Weighted collection of prefabs/models for procedural clutter placement", Icon = "grass" )]
public class ClutterIsotope : GameResource
{
	/// <summary>
	/// Collection of weighted entries. Each entry can contain a Prefab or Model with spawn parameters.
	/// </summary>
	[Property]
	public List<IsotopeEntry> Entries { get; set; } = new();

	/// <summary>
	/// Returns the number of valid entries (those with assets and positive weight).
	/// </summary>
	public int ValidEntryCount => Entries.Count( e => e is not null && e.HasAsset && e.Weight > 0 );

	/// <summary>
	/// Gets the total weight of all valid entries.
	/// </summary>
	public float TotalWeight => Entries
		.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
		.Sum( e => e.Weight );

	/// <summary>
	/// Selects a random entry based on weighted probability.
	/// Entries with higher weights have a proportionally higher chance of being selected.
	/// </summary>
	/// <returns>A random weighted entry, or null if no valid entries exist.</returns>
	public IsotopeEntry GetRandomEntry()
	{
		// Filter to valid entries only
		var validEntries = Entries
			.Where( e => e is not null && e.HasAsset && e.Weight > 0 )
			.ToList();

		if ( validEntries.Count == 0 )
			return null;

		// Calculate total weight
		var totalWeight = validEntries.Sum( e => e.Weight );
		
		// Generate random value between 0 and total weight
		var randomValue = Game.Random.Float( 0f, totalWeight );

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

		// Fallback (should never happen due to floating point precision)
		return validEntries[^1];
	}

	/// <summary>
	/// Validates all entries and returns a list of warnings/errors.
	/// Useful for editor validation.
	/// </summary>
	public List<string> Validate()
	{
		var warnings = new List<string>();

		if ( Entries.Count == 0 )
		{
			warnings.Add( "No entries defined" );
			return warnings;
		}

		var validCount = ValidEntryCount;
		if ( validCount == 0 )
		{
			warnings.Add( "No valid entries (all have weight 0 or no asset)" );
		}

		for ( int i = 0; i < Entries.Count; i++ )
		{
			var entry = Entries[i];
			if ( entry is null )
			{
				warnings.Add( $"Entry {i} is null" );
				continue;
			}

			if ( !entry.HasAsset )
			{
				warnings.Add( $"Entry {i} has no Prefab or Model assigned" );
			}

			if ( entry.Weight <= 0 )
			{
				warnings.Add( $"Entry {i} ({entry.AssetName}) has weight <= 0" );
			}
		}

		return warnings;
	}
}
