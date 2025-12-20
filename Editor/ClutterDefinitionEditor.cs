using Editor;
using Sandbox;

namespace Editor;

/// <summary>
/// Custom editor for ClutterDefinition resources.
/// Provides a specialized interface for editing clutter settings.
/// </summary>
public class ClutterDefinitionEditor : BaseResourceEditor<ClutterDefinition>
{
	protected override void Initialize( Asset asset, ClutterDefinition resource )
	{
		Layout = Layout.Column();
		Layout.Spacing = 0;
		Layout.Margin = 0;

		// Enable drag & drop on the editor window
		AcceptDrops = true;

		var serialized = resource.GetSerialized();

		// Subscribe to property changes to mark resource as dirty
		serialized.OnPropertyChanged += _ => resource.StateHasChanged();
		
		// Create main control sheet with standard layout
		var sheet = new ControlSheet();
		sheet.Margin = new Sandbox.UI.Margin( 8 );
		
		// Entries List (standard list, no custom widget)
		var entriesProperty = serialized.GetProperty( nameof( ClutterDefinition.Entries ) );
		if ( entriesProperty != null )
		{
			sheet.AddGroup( "Entries", new[] { entriesProperty } );
		}
		
		// Scatterer Group
		var scattererTypeProperty = serialized.GetProperty( nameof( ClutterDefinition.ScattererTypeName ) );
		var scattererProperty = serialized.GetProperty( nameof( ClutterDefinition.Scatterer ) );
		
		if ( scattererTypeProperty != null && scattererProperty != null )
		{
			sheet.AddGroup( "Scatterer", new[] { scattererTypeProperty, scattererProperty } );
		}
		
		// Streaming Group
		var tileSizeProperty = serialized.GetProperty( nameof( ClutterDefinition.TileSize ) );
		var tileRadiusProperty = serialized.GetProperty( nameof( ClutterDefinition.TileRadius ) );
		
		if ( tileSizeProperty != null && tileRadiusProperty != null )
		{
			sheet.AddGroup( "Streaming", new[] { tileSizeProperty, tileRadiusProperty } );
		}
		
		Layout.Add( sheet );
	}
}
