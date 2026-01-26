using Sandbox.Clutter;

namespace Editor;

/// <summary>
/// Custom editor for ClutterDefinition resources.
/// </summary>
public class ClutterDefinitionEditor : BaseResourceEditor<ClutterDefinition>
{
	protected override void Initialize( Asset asset, ClutterDefinition resource )
	{
		Layout = Layout.Column();
		Layout.Spacing = 0;
		Layout.Margin = 0;

		AcceptDrops = true;

		var serialized = resource.GetSerialized();

		serialized.OnPropertyChanged += _ => resource.StateHasChanged();

		ControlSheet sheet = new()
		{
			Margin = new Sandbox.UI.Margin( 8 )
		};

		var entriesProperty = serialized.GetProperty( nameof( ClutterDefinition.Entries ) );
		sheet.AddGroup( "Entries", [entriesProperty] );

		var scattererTypeProperty = serialized.GetProperty( nameof( ClutterDefinition.ScattererTypeName ) );
		var scattererProperty = serialized.GetProperty( nameof( ClutterDefinition.Scatterer ) );
		sheet.AddGroup( "Scatterer", [scattererTypeProperty, scattererProperty] );

		var tileSizeProperty = serialized.GetProperty( nameof( ClutterDefinition.TileSizeEnum ) );
		var tileRadiusProperty = serialized.GetProperty( nameof( ClutterDefinition.TileRadius ) );
		sheet.AddGroup( "Streaming", [tileSizeProperty, tileRadiusProperty] );

		Layout.Add( sheet );
	}
}
