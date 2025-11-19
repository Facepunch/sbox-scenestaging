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
