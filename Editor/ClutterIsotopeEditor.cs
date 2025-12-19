using Editor;
using Sandbox;

namespace Editor;

/// <summary>
/// Custom editor for ClutterIsotope resources.
/// Provides a specialized interface for editing isotope settings.
/// </summary>
public class ClutterIsotopeEditor : BaseResourceEditor<ClutterIsotope>
{
	protected override void Initialize( Asset asset, ClutterIsotope resource )
	{
		Layout = Layout.Column();
		Layout.Margin = 0;
		
		var serialized = resource.GetSerialized();
		
		// Create main control sheet
		var sheet = new ControlSheet();
		
		// Entries Group
		var entriesProperty = serialized.GetProperty( nameof( ClutterIsotope.Entries ) );
		if ( entriesProperty != null )
		{
			sheet.AddGroup( "Entries", [entriesProperty] );
		}
		
		// Scatterer Group
		var scattererTypeProperty = serialized.GetProperty( nameof( ClutterIsotope.ScattererTypeName ) );
		var scattererProperty = serialized.GetProperty( nameof( ClutterIsotope.Scatterer ) );
		var scattererPropertyTitle = ControlSheet.CreateLabel( serialized.GetProperty( nameof( ClutterIsotope.Scatterer ) ) );
		if ( scattererTypeProperty != null && scattererProperty != null )
		{
			sheet.AddGroup( "Scatterer", [scattererTypeProperty, scattererPropertyTitle, scattererProperty] );
		}
		
		// Streaming Group
		var tileSizeProperty = serialized.GetProperty( nameof( ClutterIsotope.TileSize ) );
		var tileRadiusProperty = serialized.GetProperty( nameof( ClutterIsotope.TileRadius ) );
		
		if ( tileSizeProperty != null && tileRadiusProperty != null )
		{
			sheet.AddGroup( "Streaming", [tileSizeProperty, tileRadiusProperty] );
		}
		
		Layout.Add( sheet );
	}
}
