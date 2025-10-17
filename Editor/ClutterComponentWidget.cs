using Sandbox;
using static Sandbox.ClutterInstance;

namespace Editor;

/// <summary>
/// Custom editor widget for the clutter component. Can manager layers, objects, palette and settings
/// </summary>
[CustomEditor( typeof( ClutterComponent ) )]
partial class ClutterComponentWidget : ComponentEditorWidget
{
	public ClutterComponentWidget( SerializedObject obj ) : base( obj )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();

		BuildUI();
	}

	void BuildUI()
	{
		Layout.Clear( true );

		Layout.Add( LayersPage() );
	}

	Widget LayersPage()
	{
		var container = new Widget( null );
		container.Layout = Layout.Column();

		// Main content row with side-by-side layout
		var mainRow = container.Layout.AddRow();
		mainRow.Spacing = 8;
		mainRow.Margin = 8;

		// Left side - Layers
		var leftColumn = mainRow.AddColumn();

		var layersLabel = new Label( "Clutter Layers" );
		layersLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		leftColumn.Add( layersLabel );

		var layersList = new ClutterLayersList( container, SerializedObject );
		leftColumn.Add( layersList );

		var layerActionsRow = leftColumn.AddRow();
		layerActionsRow.Margin = 8;
		layerActionsRow.Spacing = 8;
		layerActionsRow.AddStretchCell();

		var addLayerBtn = new Button( "Add Layer", "add" );
		addLayerBtn.Clicked = () => AddNewLayer( layersList );

		var removeLayerBtn = new Button( "Remove Selected", "delete" );
		removeLayerBtn.Clicked = () => RemoveSelectedLayers( layersList );

		layerActionsRow.Add( addLayerBtn );
		layerActionsRow.Add( removeLayerBtn );

		// Right side - Objects
		var rightColumn = mainRow.AddColumn();

		var objectsLabel = new Label( "Layer Objects" );
		objectsLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		rightColumn.Add( objectsLabel );

		var objectsList = new ClutterObjectsList( container, SerializedObject );
		objectsList.SetLayersList( layersList );
		layersList.SetObjectsList( objectsList );
		rightColumn.Add( objectsList );

		var addObjectsRow = rightColumn.AddRow();
		addObjectsRow.Margin = 8;
		addObjectsRow.Spacing = 8;
		addObjectsRow.AddStretchCell();

		var browseBtn = new Button( "Browse Assets...", "cloud" );
		browseBtn.Clicked = () => BrowseForAssets();

		addObjectsRow.Add( browseBtn );

		// Object Palette section - full width below
		var paletteLabel = new Label( "Object Palette" );
		paletteLabel.SetStyles( "font-weight: bold; margin: 8px;" );
		container.Layout.Add( paletteLabel );

		var clutter = SerializedObject.Targets.FirstOrDefault() as ClutterComponent;
		var palette = new ObjectPalette( container, clutter.Scene );
		palette.SetTargetObjectsList( objectsList );
		container.Layout.Add( palette );

		// Add instructions and button
		var paletteActionsRow = container.Layout.AddRow();
		paletteActionsRow.Margin = 8;
		paletteActionsRow.Spacing = 8;

		var instructionLabel = new Label( "Double-click to add to layer" );
		instructionLabel.SetStyles( "font-size: 12px; color: #888;" );
		paletteActionsRow.Add( instructionLabel );

		paletteActionsRow.AddStretchCell();

		var addToLayerBtn = new Button( "Add Selected", "add" );
		addToLayerBtn.Clicked = () => AddPaletteAssetToLayer( palette, objectsList );

		paletteActionsRow.Add( addToLayerBtn );

		return container;
	}

	void BrowseForAssets()
	{
		var clutter = SerializedObject.Targets.FirstOrDefault() as ClutterComponent;
		if ( clutter == null ) return;

		var picker = AssetPicker.Create( null, AssetType.FromExtension( "prefab" ) );
		picker.OnAssetPicked = assets =>
		{
			foreach ( var asset in assets )
			{
				//if ( !clutter.ClutterObjects.Contains( asset ) )
				//{
				//	clutter.ClutterObjects.Add( asset );
				//}
			}
			BuildUI(); // Refresh to show new assets
		};
		picker.Show();
	}

	void AddNewLayer( ClutterLayersList layersList )
	{
		var clutter = SerializedObject.Targets.FirstOrDefault() as ClutterComponent;
		if ( clutter == null ) return;

		var newLayer = new ClutterLayer()
		{
			Name = $"Layer {clutter.Layers.Count + 1}",
			Objects = []
		};

		clutter.Layers.Add( newLayer );
		layersList.BuildItems(); // Just refresh the layers list
		layersList.SelectLayerByInstance( newLayer ); // Select the newly added layer
	}

	void RemoveSelectedLayers( ClutterLayersList layersList )
	{
		var clutter = SerializedObject.Targets.FirstOrDefault() as ClutterComponent;
		if ( clutter == null ) return;

		var selectedLayers = layersList.GetSelectedLayers();
		var firstSelectedIndex = layersList.GetFirstSelectedIndex();

		foreach ( var layer in selectedLayers )
		{
			clutter.Layers.Remove( layer );
		}

		layersList.BuildItems(); // Just refresh the layers list
		layersList.SelectNextAfterDeletion( firstSelectedIndex ); // Select next layer after deletion
	}

	void AddPaletteAssetToLayer( ObjectPalette palette, ClutterObjectsList objectsList )
	{
		var selectedAsset = palette.GetSelectedAsset();
		if ( selectedAsset != null )
		{
			objectsList.AddAssetFromPalette( selectedAsset );
			Log.Info( $"Added {selectedAsset.Name} to layer objects" );
		}
		else
		{
			Log.Info( "No asset selected in palette" );
		}
	}
}
