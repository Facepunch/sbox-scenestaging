using Sandbox;
using System.Collections.Generic;
using static Sandbox.ClothingContainer;

[Icon( "checkroom", "blue", "white" )]
[EditorHandle( "editor/citizenhead.png" )]
public sealed class ClothingFileDresser : Component
{
	// New struct to hold clothing and source together
	public struct ClothingSet
	{
		public List<Clothing> Clothes { get; set; }
		public SkinnedModelRenderer Source { get; set; }
		public bool IsHuman { get; set; }
	}

	[Property, InlineEditor] List<ClothingSet> Sets { get; set; } = new();

	[Button( "Dress" )]
	void DressCitizen()
	{
		foreach ( var set in Sets )
		{
			if ( set.Source == null || !set.Source.IsValid() )
				continue;

			var container = new ClothingContainer();
			container.PrefersHuman = set.IsHuman;
			container.Reset( set.Source );
			container.Clothing.Clear();

			foreach ( var clothing in set.Clothes )
			{
				if ( clothing is null )
					continue;

				var entry = new ClothingEntry( clothing );
				if ( container.Clothing.Contains( entry ) )
					continue;

				container.Clothing.Add( entry );
			}

			container.Normalize();
			container.Apply( set.Source );
		}
	}

	[Button( "UnDress" )]
	void UnDressCitizen()
	{
		foreach ( var set in Sets )
		{
			if ( set.Source == null || !set.Source.IsValid() )
				continue;

			var container = new ClothingContainer();
			container.Reset( set.Source );
			container.Clothing.Clear();
		}
	}
}
