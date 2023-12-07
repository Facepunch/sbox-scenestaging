using Sandbox.TerrainEngine;

namespace Editor.TerrainEngine;

public partial class TerrainComponentWidget
{
	Widget SettingsPage()
	{
		var container = new Widget( null );

		var sheet = new ControlSheet();

		var props = SerializedObject.Where( x => x.HasAttribute<PropertyAttribute>() )
							.OrderBy( x => x.SourceLine )
							.ThenBy( x => x.DisplayName )
							.ToArray();

		foreach ( var prop in props )
		{
			sheet.AddRow( prop );
		}

		//
		// Terrain Data
		//

		sheet.AddRow( SerializedTerrainData.GetProperty( nameof( TerrainData.HeightMapSize ) ) );
		sheet.AddRow( SerializedTerrainData.GetProperty( nameof( TerrainData.MaxHeight ) ) );

		container.Layout = Layout.Column();
		container.Layout.Add( sheet );

		var warning = new WarningBox( "Heightmaps must use a single channel and be either 8 or 16 bit. Resolution must be power of two." );
		container.Layout.Add( warning );
		container.Layout.Add( new Button( "Import Heightmap" ) );

		return container;
	}
}
