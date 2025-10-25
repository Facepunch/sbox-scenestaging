using System;
using static Sandbox.ClutterInstance;

namespace Editor;

[CustomEditor( typeof( List<ClutterLayer> ), NamedEditor = "clutter_layer_multi" )]
public class ClutterLayerMultiControlWidget : ControlWidget, AssetSystem.IEventListener
{
	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlButton => true;
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();

	PopupWidget _menu;
	List<ClutterLayer> _cachedLayers;
	bool _needsRebuild = true;
	ScatterBrush _lastBrush;

	// IEventListener implementation
	void AssetSystem.IEventListener.OnAssetChanged( Asset asset )
	{
		// Invalidate cache if the ScatterBrush asset was changed
		if ( _lastBrush != null && asset.AbsolutePath == _lastBrush.ResourcePath )
		{
			// Force reload the brush resource from disk
			if ( asset.TryLoadResource<ScatterBrush>( out var reloadedBrush ) )
			{
				// Update the property with the reloaded resource
				var brushProperty = SerializedProperty.Parent?.GetProperty( "Brush" );
				if ( brushProperty != null )
				{
					brushProperty.SetValue( reloadedBrush );
				}

				_lastBrush = reloadedBrush;
			}

			_needsRebuild = true;
			Update(); // Force a repaint
		}
	}

	void AssetSystem.IEventListener.OnAssetSystemChanges() { }
	void AssetSystem.IEventListener.OnAssetTagsChanged() { }

	// Access to the internal _selectedLayerIds field for stable persistence (ClutterVolumeComponent)
	// Returns null if it doesn't exist (ScatterTool)
	SerializedProperty InternalIdsProperty => SerializedProperty.Parent?.GetProperty( "_selectedLayerIds" );

	// Whether we're using the internal IDs approach (true) or direct layer list (false)
	bool UseInternalIds => InternalIdsProperty != null;

	public ClutterLayerMultiControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	public void InvalidateCache()
	{
		_needsRebuild = true;
		_cachedLayers = null;
	}

	List<ClutterLayer> GetAvailableLayers()
	{
		// Check if brush changed
		ScatterBrush currentBrush = null;
		if ( SerializedProperty.Parent != null )
		{
			var targetObject = SerializedProperty.Parent.Targets.FirstOrDefault();
			if ( targetObject is ClutterVolumeComponent volume )
			{
				currentBrush = volume.Brush;
			}
			else if ( targetObject is Editor.TerrainTool.InspectorSettings inspectorSettings )
			{
				currentBrush = inspectorSettings.Brush;
			}
		}

		// Invalidate cache if brush changed
		if ( currentBrush != _lastBrush )
		{
			_needsRebuild = true;
			_lastBrush = currentBrush;
		}

		// Get all layers from the ClutterSystem and any assigned brush
		var activeScene = SceneEditorSession.Active?.Scene;
		List<ClutterLayer> enabledLayers = [];
		if ( activeScene != null )
		{
			var clutterSystem = activeScene.GetSystem<ClutterSystem>();
			if ( clutterSystem != null )
			{
				enabledLayers.AddRange( clutterSystem.GetAllLayers() );
			}
		}

		// Also include layers from brush resource
		if ( currentBrush != null )
		{
			if ( currentBrush.Layers != null )
			{
				// Add brush layers that aren't already in the list (by ID)
				foreach ( var brushLayer in currentBrush.Layers )
				{
					if ( !enabledLayers.Any( l => l.Id == brushLayer.Id ) )
					{
						enabledLayers.Add( brushLayer );
					}
				}
			}
		}

		// Check if layer names or objects have changed by comparing with cache
		if ( _cachedLayers != null && !_needsRebuild )
		{
			// Check if counts match
			if ( _cachedLayers.Count != enabledLayers.Count )
			{
				_needsRebuild = true;
			}
			else
			{
				// Check if any layer names or object counts changed
				for ( int i = 0; i < enabledLayers.Count; i++ )
				{
					var newLayer = enabledLayers[i];
					var cachedLayer = _cachedLayers.FirstOrDefault( l => l.Id == newLayer.Id );

					if ( cachedLayer == null ||
					     cachedLayer.Name != newLayer.Name ||
					     cachedLayer.Objects?.Count != newLayer.Objects?.Count )
					{
						_needsRebuild = true;
						break;
					}
				}
			}
		}

		if ( _cachedLayers != null && !_needsRebuild )
		{
			return _cachedLayers;
		}

		_cachedLayers = enabledLayers;
		_needsRebuild = false;
		return _cachedLayers;
	}

	protected override void PaintControl()
	{
		var enabledLayers = GetAvailableLayers();

		// Get selected layer IDs - either from internal storage or from the layer list itself
		List<Guid> selectedIds;
		if ( UseInternalIds )
		{
			selectedIds = InternalIdsProperty.GetValue<List<Guid>>() ?? [];
		}
		else
		{
			var layers = SerializedProperty.GetValue<List<ClutterLayer>>() ?? [];
			selectedIds = layers.Select( l => l.Id ).ToList();
		}

		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled ) color = color.WithAlpha( 0.5f );

		var rect = LocalRect.Shrink( 8, 0 );

		if ( enabledLayers.Count > 0 )
		{
			// Match by ID
			var selectedLayers = enabledLayers.Where( l => selectedIds.Contains( l.Id ) ).ToList();

			string displayText;
			if ( selectedLayers.Count == 0 )
			{
				displayText = "No Layers";
			}
			else if ( selectedLayers.Count == enabledLayers.Count )
			{
				displayText = "All Layers";
			}
			else if ( selectedLayers.Count == 1 )
			{
				displayText = selectedLayers[0].Name;
			}
			else
			{
				displayText = string.Join( ", ", selectedLayers.Select( l => l.Name ) );
			}

			Paint.SetPen( color );
			Paint.DrawText( rect, displayText, TextFlag.LeftCenter );
		}
		else
		{
			Paint.SetPen( color.WithAlpha( 0.5f ) );
			Paint.DrawText( rect, "No Layers", TextFlag.LeftCenter );
		}

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled ) return;

		if ( !_menu.IsValid )
		{
			OpenMenu();
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( IsControlDisabled ) return;

		if ( e.LeftMouseButton && !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	void ToggleLayer( Guid layerId )
	{
		if ( UseInternalIds )
		{
			// Use internal IDs storage (ClutterVolumeComponent)
			var currentIds = InternalIdsProperty.GetValue<List<Guid>>() ?? new List<Guid>();

			if ( currentIds.Contains( layerId ) )
			{
				currentIds.Remove( layerId );
			}
			else
			{
				currentIds.Add( layerId );
			}

			InternalIdsProperty.SetValue( currentIds );
		}
		else
		{
			// Use direct layer list (ScatterTool)
			var currentLayers = SerializedProperty.GetValue<List<ClutterLayer>>() ?? new List<ClutterLayer>();
			var allLayers = GetAvailableLayers();

			// Find the layer by ID
			var existingLayer = currentLayers.FirstOrDefault( l => l.Id == layerId );

			if ( existingLayer != null )
			{
				currentLayers.Remove( existingLayer );
			}
			else
			{
				var layerToAdd = allLayers.FirstOrDefault( l => l.Id == layerId );
				if ( layerToAdd != null )
				{
					currentLayers.Add( layerToAdd );
				}
			}

			SerializedProperty.SetValue( currentLayers );
		}
	}

	void OpenMenu()
	{
		PropertyStartEdit();

		var enabledLayers = GetAvailableLayers();

		if ( enabledLayers.Count == 0 )
		{
			return;
		}

		_menu = new PopupWidget( null );
		_menu.Layout = Layout.Column();
		_menu.MinimumWidth = ScreenRect.Width;
		_menu.MaximumWidth = ScreenRect.Width;
		_menu.OnLostFocus += PropertyFinishEdit;

		var scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		scroller.Canvas = new Widget( scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			MaximumWidth = ScreenRect.Width
		};

		foreach ( var layer in enabledLayers )
		{
			var option = scroller.Canvas.Layout.Add( new LayerMenuOption( layer, () =>
			{
				// Get selected IDs based on storage type
				List<Guid> currentIds;
				if ( UseInternalIds )
				{
					currentIds = InternalIdsProperty?.GetValue<List<Guid>>() ?? [];
				}
				else
				{
					var layers = SerializedProperty.GetValue<List<ClutterLayer>>() ?? [];
					currentIds = layers.Select( l => l.Id ).ToList();
				}
				return currentIds.Contains( layer.Id );
			} ) );
			option.MouseClick = () =>
			{
				ToggleLayer( layer.Id );
				_menu.Update();
			};
		}

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
		_menu.OnPaintOverride = PaintMenuBackground;

		if ( scroller.VerticalScrollbar.Minimum != scroller.VerticalScrollbar.Maximum )
		{
			scroller.Canvas.MaximumWidth -= 8; // leave some space for the scrollbar
		}
	}

	bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( Paint.LocalRect, 0 );
		return true;
	}
}

file class LayerMenuOption : Widget
{
	ClutterLayer layer;
	Func<bool> isSelectedFunc;

	public LayerMenuOption( ClutterLayer l, Func<bool> isSelectedFunction ) : base( null )
	{
		layer = l;
		isSelectedFunc = isSelectedFunction;

		Layout = Layout.Row();
		Layout.Margin = 8;
		VerticalSizeMode = SizeMode.CanGrow;
		Cursor = CursorShape.Finger;

		var icon = new IconButton( "layers" ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 18 };
		icon.Cursor = CursorShape.Finger;
		Layout.Add( icon );
		Layout.AddSpacingCell( 8 );

		var c = Layout.AddColumn();
		var title = c.Add( new Label( layer.Name ) );
		title.SetStyles( $"font-size: 12px; font-weight: bold; font-family: {Theme.DefaultFont}; color: white;" );

		var objectCount = layer.Objects?.Count ?? 0;
		var desc = c.Add( new Label( $"{objectCount} objects" ) );
		desc.SetStyles( $"font-size: 11px; font-family: {Theme.DefaultFont}; color: #aaa;" );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton )
		{
			MouseLeftPress?.Invoke();
			e.Accepted = true;
			Update();
		}

		base.OnMouseClick( e );
	}

	protected override void OnPaint()
	{
		var isSelected = isSelectedFunc?.Invoke() ?? false;
		if ( Paint.HasMouseOver || isSelected )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( isSelected ? 0.3f : 0.1f ) );
			Paint.DrawRect( LocalRect.Shrink( 2 ), 2 );
		}
	}
}
