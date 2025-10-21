using Sandbox;

namespace Editor;

/// <summary>
/// Custom editor widget for ScatterBrush resource that includes full layer configuration UI
/// </summary>
[CustomEditor( typeof( ScatterBrush ) )]
public class ScatterBrushEditor : ResourceControlWidget
{
	private SerializedObject _serializedBrush;
	private ClutterLayersList _layersList;
	private ClutterObjectsList _objectsList;
	private ObjectPalette _palette;

	public ScatterBrushEditor( SerializedProperty property ) : base( property )
	{
		// Create a serialized object for the brush resource
		var brush = property.GetValue<ScatterBrush>( null );
		if ( brush != null )
		{
			_serializedBrush = EditorTypeLibrary.GetSerializedObject( brush );
		}
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		// Draw the resource preview badge
		var brush = SerializedProperty.GetValue<ScatterBrush>( null );
		if ( brush != null && brush.Layers.Count > 0 )
		{
			var rect = new Rect( Width - 30, 5, 25, 15 );
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.8f ) );
			Paint.ClearPen();
			Paint.DrawRect( rect, 2 );

			Paint.SetPen( Color.White );
			Paint.SetDefaultFont( 7 );
			Paint.DrawText( rect, $"{brush.Layers.Count}", TextFlag.Center );
		}
	}

	protected override void BuildControlPopup( Widget popup )
	{
		popup.SetSizeMode( SizeMode.CanShrink, SizeMode.CanShrink );
		popup.MinimumWidth = 800;
		popup.MinimumHeight = 600;

		var layout = Layout.Column();
		popup.Layout = layout;
		layout.Margin = 16;
		layout.Spacing = 8;

		// Title
		var titleRow = layout.AddRow();
		titleRow.Spacing = 8;

		var brush = SerializedProperty.GetValue<ScatterBrush>( null );
		var titleLabel = new Label( $"Scatter Brush: {brush?.DisplayName ?? "Unnamed"}" );
		titleLabel.SetStyles( "font-weight: bold; font-size: 14px;" );
		titleRow.Add( titleLabel );
		titleRow.AddStretchCell();

		// Display Name Editor
		var nameRow = layout.AddRow();
		nameRow.Spacing = 8;
		nameRow.Add( new Label( "Display Name:" ) { MinimumWidth = 100 } );

		var nameInput = new LineEdit();
		nameInput.Text = brush?.DisplayName ?? "";
		nameInput.TextEdited = ( text ) =>
		{
			if ( brush != null )
			{
				brush.DisplayName = text;
				brush.SaveToDisk();
			}
		};
		nameRow.Add( nameInput );

		layout.Add( new Widget() { MinimumHeight = 8 } ); // Spacer

		// Main content row with side-by-side layout
		var mainRow = layout.AddRow();
		mainRow.Spacing = 8;

		// Left side - Layers
		var leftColumn = mainRow.AddColumn();
		leftColumn.MinimumWidth = 250;

		var layersLabel = new Label( "Clutter Layers" );
		layersLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		leftColumn.Add( layersLabel );

		if ( _serializedBrush != null )
		{
			_layersList = new ClutterLayersList( popup, _serializedBrush );
			leftColumn.Add( _layersList );
		}

		var layerActionsRow = leftColumn.AddRow();
		layerActionsRow.Margin = 8;
		layerActionsRow.Spacing = 8;
		layerActionsRow.AddStretchCell();

		var addLayerBtn = new Button( "Add Layer", "add" );
		addLayerBtn.Clicked = () => AddNewLayer();

		var removeLayerBtn = new Button( "Remove Selected", "delete" );
		removeLayerBtn.Clicked = () => RemoveSelectedLayers();

		layerActionsRow.Add( addLayerBtn );
		layerActionsRow.Add( removeLayerBtn );

		// Right side - Objects
		var rightColumn = mainRow.AddColumn();
		rightColumn.MinimumWidth = 300;

		var objectsLabel = new Label( "Layer Objects" );
		objectsLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		rightColumn.Add( objectsLabel );

		if ( _serializedBrush != null )
		{
			_objectsList = new ClutterObjectsList( popup, _serializedBrush );
			_objectsList.SetLayersList( _layersList );
			_layersList?.SetObjectsList( _objectsList );
			rightColumn.Add( _objectsList );
		}

		var addObjectsRow = rightColumn.AddRow();
		addObjectsRow.Margin = 8;
		addObjectsRow.Spacing = 8;
		addObjectsRow.AddStretchCell();

		var browseBtn = new Button( "Browse Assets...", "cloud" );
		browseBtn.Clicked = () => BrowseForAssets();

		addObjectsRow.Add( browseBtn );

		// Object Palette section - full width below
		var paletteLabel = new Label( "Object Palette" );
		paletteLabel.SetStyles( "font-weight: bold; margin: 8px 0px;" );
		layout.Add( paletteLabel );

		// Note: ObjectPalette currently expects a Scene, but for ScatterBrush we need to adapt it
		// For now we'll create a simplified version or pass null and handle gracefully
		_palette = new BrushObjectPalette( popup, brush );
		_palette.SetTargetObjectsList( _objectsList );
		layout.Add( _palette );

		// Add instructions and button
		var paletteActionsRow = layout.AddRow();
		paletteActionsRow.Margin = 8;
		paletteActionsRow.Spacing = 8;

		var instructionLabel = new Label( "Double-click to add to layer" );
		instructionLabel.SetStyles( "font-size: 12px; color: #888;" );
		paletteActionsRow.Add( instructionLabel );

		paletteActionsRow.AddStretchCell();

		var addToLayerBtn = new Button( "Add Selected", "add" );
		addToLayerBtn.Clicked = () => AddPaletteAssetToLayer();

		paletteActionsRow.Add( addToLayerBtn );

		// Save button at the bottom
		var saveRow = layout.AddRow();
		saveRow.Margin = 16;
		saveRow.Spacing = 8;
		saveRow.AddStretchCell();

		var saveBtn = new Button( "Save Changes", "save" );
		saveBtn.Clicked = () =>
		{
			brush?.SaveToDisk();
			Log.Info( $"Saved scatter brush: {brush?.DisplayName}" );
		};
		saveRow.Add( saveBtn );
	}

	void AddNewLayer()
	{
		var brush = SerializedProperty.GetValue<ScatterBrush>( null );
		if ( brush == null ) return;

		var newLayer = new ClutterLayer()
		{
			Name = $"Layer {brush.Layers.Count + 1}",
			Objects = []
		};

		brush.Layers.Add( newLayer );
		brush.SaveToDisk();

		_layersList?.BuildItems();
		_layersList?.SelectLayerByInstance( newLayer );
	}

	void RemoveSelectedLayers()
	{
		var brush = SerializedProperty.GetValue<ScatterBrush>( null );
		if ( brush == null ) return;

		if ( _layersList != null )
		{
			var selectedLayers = _layersList.GetSelectedLayers();
			var firstSelectedIndex = _layersList.GetFirstSelectedIndex();

			foreach ( var layer in selectedLayers )
			{
				brush.Layers.Remove( layer );
			}

			brush.SaveToDisk();

			_layersList.BuildItems();
			_layersList.SelectNextAfterDeletion( firstSelectedIndex );
		}
	}

	void BrowseForAssets()
	{
		var picker = AssetPicker.Create( null, AssetType.FromExtension( "prefab" ) );
		picker.OnAssetPicked = assets =>
		{
			// Assets can be added directly via the palette
			foreach ( var asset in assets )
			{
				_palette?.AddAssetToPaletteIfNotExists( asset );
			}
		};
		picker.Show();
	}

	void AddPaletteAssetToLayer()
	{
		if ( _palette != null && _objectsList != null )
		{
			var selectedAsset = _palette.GetSelectedAsset();
			if ( selectedAsset != null )
			{
				_objectsList.AddAssetFromPalette( selectedAsset );

				var brush = SerializedProperty.GetValue<ScatterBrush>( null );
				brush?.SaveToDisk();

				Log.Info( $"Added {selectedAsset.Name} to layer objects" );
			}
			else
			{
				Log.Info( "No asset selected in palette" );
			}
		}
	}
}

/// <summary>
/// Object palette adapted for ScatterBrush (works without requiring a Scene)
/// </summary>
public class BrushObjectPalette : ObjectPalette
{
	public BrushObjectPalette( Widget parent, ScatterBrush brush ) : base( parent, null )
	{
		// Override the base initialization to work with ScatterBrush instead of Scene
		PaletteAssets.Clear();

		if ( brush != null )
		{
			foreach ( var layer in brush.Layers )
			{
				if ( layer.Objects != null )
				{
					foreach ( var obj in layer.Objects )
					{
						if ( obj.Path is string path )
						{
							var asset = AssetSystem.FindByPath( path );
							if ( asset != null && !PaletteAssets.Contains( asset ) )
							{
								PaletteAssets.Add( asset );
							}
						}
					}
				}
			}
		}

		SetItems( PaletteAssets.Cast<object>().ToList() );
	}
}
