using Sandbox;

namespace Editor;

/// <summary>
/// Full resource editor for ScatterBrush - displayed when opening the asset
/// </summary>
public class ScatterBrushResourceEditor : BaseResourceEditor<ScatterBrush>
{
	private SerializedObject _serializedBrush;
	private ClutterLayersList _layersList;
	private ClutterObjectsList _objectsList;
	private ObjectPalette _palette;

	public ScatterBrushResourceEditor()
	{
		Layout = Layout.Column();
		MinimumHeight = 600;
		MinimumWidth = 800;
	}

	protected override void Initialize( Asset asset, ScatterBrush resource )
	{
		Layout.Clear( true );

		_serializedBrush = resource.GetSerialized();

		BuildUI();
	}

	void BuildUI()
	{
		// Main content row with side-by-side layout
		var mainRow = Layout.AddRow();
		mainRow.Spacing = 8;
		mainRow.Margin = 8;

		// Left side - Layers
		var leftColumn = mainRow.AddColumn();

		var layersLabel = new Label( "Clutter Layers" );
		layersLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		leftColumn.Add( layersLabel );

		_layersList = new ClutterLayersList( this, _serializedBrush );
		_layersList.OnChanged = () => NoteChanged(); // Hook up change notification
		leftColumn.Add( _layersList );

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

		var objectsLabel = new Label( "Layer Objects" );
		objectsLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		rightColumn.Add( objectsLabel );

		_objectsList = new ClutterObjectsList( this, _serializedBrush );
		_objectsList.OnChanged = () => NoteChanged(); // Hook up change notification
		_objectsList.SetLayersList( _layersList );
		_layersList.SetObjectsList( _objectsList );
		rightColumn.Add( _objectsList );

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
		Layout.Add( paletteLabel );

		_palette = new BrushObjectPalette( this, Resource );
		_palette.SetTargetObjectsList( _objectsList );
		Layout.Add( _palette );

		// Add instructions and button
		var paletteActionsRow = Layout.AddRow();
		paletteActionsRow.Margin = 8;
		paletteActionsRow.Spacing = 8;

		var instructionLabel = new Label( "Double-click to add to layer" );
		instructionLabel.SetStyles( "font-size: 12px; color: #888;" );
		paletteActionsRow.Add( instructionLabel );

		paletteActionsRow.AddStretchCell();

		var addToLayerBtn = new Button( "Add Selected", "add" );
		addToLayerBtn.Clicked = () => AddPaletteAssetToLayer();

		paletteActionsRow.Add( addToLayerBtn );
	}

	void AddNewLayer()
	{
		if ( Resource == null ) return;

		var newLayer = new ClutterLayer()
		{
			Name = $"Layer {Resource.Layers.Count + 1}",
			Objects = []
		};

		Resource.Layers.Add( newLayer );
		NoteChanged(); // Notify the editor that changes were made

		_layersList?.BuildItems();
		_layersList?.SelectLayerByInstance( newLayer );
	}

	void RemoveSelectedLayers()
	{
		if ( Resource == null || _layersList == null ) return;

		var selectedLayers = _layersList.GetSelectedLayers();
		var firstSelectedIndex = _layersList.GetFirstSelectedIndex();

		foreach ( var layer in selectedLayers )
		{
			Resource.Layers.Remove( layer );
		}

		NoteChanged(); // Notify the editor that changes were made

		_layersList.BuildItems();
		_layersList.SelectNextAfterDeletion( firstSelectedIndex );
	}

	void BrowseForAssets()
	{
		var picker = AssetPicker.Create( null, AssetType.FromExtension( "prefab" ) );
		picker.OnAssetPicked = assets =>
		{
			foreach ( var asset in assets )
			{
				_palette?.AddAssetToPaletteIfNotExists( asset );
			}
			NoteChanged(); // Notify the editor that changes were made
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
				NoteChanged(); // Notify the editor that changes were made
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
/// Custom editor widget for ScatterBrush resource - opens full layer editor on double-click
/// </summary>
[CustomEditor( typeof( ScatterBrush ) )]
public class ScatterBrushControlWidget : ResourceControlWidget
{
	public ScatterBrushControlWidget( SerializedProperty property ) : base( property )
	{
	}

	protected override void PaintControl()
	{
		base.PaintControl();

		// Draw layer count badge
		var brush = SerializedProperty.GetValue<ScatterBrush>( null );
		if ( brush != null && brush.Layers.Count > 0 )
		{
			var rect = new Rect( Width - 30, Height - 20, 25, 15 );
			Paint.SetBrush( Theme.Blue.WithAlpha( 0.8f ) );
			Paint.ClearPen();
			Paint.DrawRect( rect, 2 );

			Paint.SetPen( Color.White );
			Paint.SetDefaultFont( 7 );
			Paint.DrawText( rect, $"{brush.Layers.Count}", TextFlag.Center );
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		// Open editor on double-click
		if ( e.IsDoubleClick )
		{
			var brush = SerializedProperty.GetValue<ScatterBrush>( null );
			if ( brush != null )
			{
				var editor = new ScatterBrushEditorWindow( brush );
				editor.Show();
				e.Accepted = true;
				return;
			}
		}

		base.OnMouseClick( e );
	}
}

/// <summary>
/// Full editor window for configuring ScatterBrush layers and objects
/// </summary>
public class ScatterBrushEditorWindow : Dialog
{
	private ScatterBrush _brush;
	private SerializedObject _serializedBrush;
	private ClutterLayersList _layersList;
	private ClutterObjectsList _objectsList;
	private ObjectPalette _palette;

	public ScatterBrushEditorWindow( ScatterBrush brush ) : base( null )
	{
		_brush = brush;
		_serializedBrush = EditorTypeLibrary.GetSerializedObject( brush );

		WindowTitle = $"Scatter Brush: {brush?.DisplayName ?? "Unnamed"}";
		Size = new Vector2( 1000, 700 );
		MinimumSize = new Vector2( 800, 600 );

		BuildUI();
	}

	void BuildUI()
	{
		var layout = Layout.Column();

		// Main content row with side-by-side layout (matching ClutterComponentWidget)
		var mainRow = layout.AddRow();
		mainRow.Spacing = 8;
		mainRow.Margin = 8;

		// Left side - Layers
		var leftColumn = mainRow.AddColumn();

		var layersLabel = new Label( "Clutter Layers" );
		layersLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		leftColumn.Add( layersLabel );

		_layersList = new ClutterLayersList( this, _serializedBrush );
		// Note: EditorWindow doesn't have NoteChanged, so no callback here
		leftColumn.Add( _layersList );

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

		var objectsLabel = new Label( "Layer Objects" );
		objectsLabel.SetStyles( "font-weight: bold; margin-bottom: 8px;" );
		rightColumn.Add( objectsLabel );

		_objectsList = new ClutterObjectsList( this, _serializedBrush );
		// Note: EditorWindow doesn't have NoteChanged, so no callback here
		_objectsList.SetLayersList( _layersList );
		_layersList.SetObjectsList( _objectsList );
		rightColumn.Add( _objectsList );

		var addObjectsRow = rightColumn.AddRow();
		addObjectsRow.Margin = 8;
		addObjectsRow.Spacing = 8;
		addObjectsRow.AddStretchCell();

		var browseBtn = new Button( "Browse Assets...", "cloud" );
		browseBtn.Clicked = () => BrowseForAssets();

		addObjectsRow.Add( browseBtn );

		// Object Palette section - full width below (matching ClutterComponentWidget)
		var paletteLabel = new Label( "Object Palette" );
		paletteLabel.SetStyles( "font-weight: bold; margin: 8px;" );
		layout.Add( paletteLabel );

		_palette = new BrushObjectPalette( this, _brush );
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
	}

	void AddNewLayer()
	{
		if ( _brush == null ) return;

		var newLayer = new ClutterLayer()
		{
			Name = $"Layer {_brush.Layers.Count + 1}",
			Objects = []
		};

		_brush.Layers.Add( newLayer );

		_layersList?.BuildItems();
		_layersList?.SelectLayerByInstance( newLayer );
	}

	void RemoveSelectedLayers()
	{
		if ( _brush == null || _layersList == null ) return;

		var selectedLayers = _layersList.GetSelectedLayers();
		var firstSelectedIndex = _layersList.GetFirstSelectedIndex();

		foreach ( var layer in selectedLayers )
		{
			_brush.Layers.Remove( layer );
		}

		_layersList.BuildItems();
		_layersList.SelectNextAfterDeletion( firstSelectedIndex );
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
