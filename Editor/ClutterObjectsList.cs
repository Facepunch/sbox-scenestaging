using Sandbox;
using System;
using static Sandbox.ClutterInstance;

namespace Editor;

/// <summary>
/// Represents a list of clutter objects for a layer in the UI
/// </summary>
public class ClutterObjectsList : ListView
{
	private SerializedObject _serializedObject;
	private ClutterLayersList _layersList;
	private List<Asset> _currentLayerAssets = new();


	private bool _isDraggingSlider = false;
	private Asset _draggedSliderAsset = null;

	public ClutterObjectsList( Widget parent, SerializedObject serializedObject ) : base( parent )
	{
		ItemContextMenu = ShowItemContext;
		ItemActivated = OnItemDoubleClicked;
		Margin = 8;
		ItemSpacing = 4;
		AcceptDrops = true;
		MinimumHeight = 150;
		MultiSelect = true;

		ItemSize = new Vector2( 68, 68 + 16 + 24 ); // Added 24px for slider
		ItemAlign = Sandbox.UI.Align.FlexStart;

		_serializedObject = serializedObject;

		BuildItems();
	}


	protected void OnItemDoubleClicked( object obj )
	{
		if ( obj is not Asset asset ) return;
		asset.OpenInEditor();
	}

	public void BuildItems()
	{
		SetItems( _currentLayerAssets.Cast<object>().ToList() );
	}

	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not Asset asset )
			return;

		// Main asset area (thumbnail + name)
		var assetRect = new Rect( item.Rect.Left, item.Rect.Top, item.Rect.Width, 68 + 16 );
		var thumbnailRect = new Rect( assetRect.Left, assetRect.Top, assetRect.Width, 68 );

		if ( item.Selected || Paint.HasMouseOver )
		{
			Paint.SetBrush( Theme.Blue.WithAlpha( item.Selected ? 0.5f : 0.2f ) );
			Paint.ClearPen();
			Paint.DrawRect( assetRect, 4 );
		}

		var pixmap = asset.GetAssetThumb();
		Paint.Draw( thumbnailRect.Shrink( 2 ), pixmap );

		// Draw type icon
		var path = asset.Path?.ToLower();
		var iconRect = new Rect( assetRect.Right - 18, assetRect.Top + 2, 16, 16 );
		Paint.SetPen( Color.White.WithAlpha( 0.8f ), 1 );

		if ( path?.EndsWith( ".prefab" ) == true )
		{
			Paint.DrawIcon( iconRect, "view_in_ar", 12 );
		}
		else if ( path?.EndsWith( ".vmdl" ) == true )
		{
			Paint.DrawIcon( iconRect, "category", 12 );
		}

		// Draw small clutter indicator
		if ( IsAssetMarkedAsSmall( asset ) )
		{
			var smallIconRect = new Rect( assetRect.Right - 36, assetRect.Top + 2, 16, 16 );
			Paint.SetPen( Theme.Yellow, 1 );
			Paint.DrawIcon( smallIconRect, "adjust", 10 );
		}

		Paint.SetDefaultFont();
		Paint.SetPen( Theme.Text );
		Paint.DrawText( assetRect.Shrink( 2 ), asset.Name, TextFlag.CenterBottom );

		// Draw weight slider
		var sliderRect = new Rect( item.Rect.Left + 4, assetRect.Bottom + 2, item.Rect.Width - 8, 20 );
		PaintWeightSlider( sliderRect, asset );
	}

	private void PaintWeightSlider( Rect rect, Asset asset )
	{
		// Get weight for this asset
		float weight = GetAssetWeight( asset );

		// Draw track
		var trackHeight = 4;
		var trackRect = new Rect( rect.Left + 8, rect.Center.y - trackHeight / 2, rect.Width - 16, trackHeight );
		Paint.SetBrush( Theme.ControlBackground.Darken( 0.2f ) );
		Paint.ClearPen();
		Paint.DrawRect( trackRect, 2 );

		// Draw filled portion
		if ( weight > 0 )
		{
			var fillRect = new Rect( trackRect.Left, trackRect.Top, trackRect.Width * weight, trackRect.Height );
			Paint.SetBrush( Theme.Blue );
			Paint.DrawRect( fillRect, 2 );
		}

		// Draw handle
		var handleSize = 12;
		var handleX = trackRect.Left + (trackRect.Width * weight) - handleSize / 2;
		var handle = new Rect( handleX, rect.Center.y - handleSize / 2, handleSize, handleSize );

		Paint.SetBrush( Theme.Blue );
		Paint.SetPen( Theme.Text.WithAlpha( 0.3f ), 1 );
		Paint.DrawCircle( handle );

		// Draw weight text
		Paint.SetDefaultFont( 8 );
		Paint.SetPen( Theme.Text );
		var textRect = new Rect( rect.Right - 30, rect.Top, 30, rect.Height );
		Paint.DrawText( textRect, $"{weight:F2}", TextFlag.RightCenter );
	}

	private float GetAssetWeight( Asset asset )
	{
		if ( _layersList != null )
		{
			var selectedLayers = _layersList.GetSelectedLayers();
			if ( selectedLayers.Count > 0 )
			{
				var selectedLayer = selectedLayers.First();
				var clutterObj = selectedLayer.Objects.FirstOrDefault( obj => obj.Path == asset.Path );
				return clutterObj.Weight;
			}
		}
		return 1.0f;
	}

	private void SetAssetWeight( Asset asset, float weight )
	{
		if ( _layersList != null )
		{
			var selectedLayers = _layersList.GetSelectedLayers();
			if ( selectedLayers.Count > 0 )
			{
				var selectedLayer = selectedLayers.First();
				for ( int i = 0; i < selectedLayer.Objects.Count; i++ )
				{
					if ( selectedLayer.Objects[i].Path == asset.Path )
					{
						var clutterObj = selectedLayer.Objects[i];
						clutterObj.Weight = Math.Clamp( weight, 0.0f, 1.0f );
						selectedLayer.Objects[i] = clutterObj;
						Update(); // Repaint to show new weight
						break;
					}
				}
			}
		}
	}

	private bool IsPointInSliderArea( Vector2 point, VirtualWidget item )
	{
		if ( item.Object is not Asset ) return false;

		var assetRect = new Rect( item.Rect.Left, item.Rect.Top, item.Rect.Width, 68 + 16 );
		var sliderRect = new Rect( item.Rect.Left + 4, assetRect.Bottom + 2, item.Rect.Width - 8, 20 );
		return point.x >= sliderRect.Left && point.x <= sliderRect.Right &&
			   point.y >= sliderRect.Top && point.y <= sliderRect.Bottom;
	}

	private float GetWeightFromSliderPosition( Vector2 point, VirtualWidget item )
	{
		var assetRect = new Rect( item.Rect.Left, item.Rect.Top, item.Rect.Width, 68 + 16 );
		var sliderRect = new Rect( item.Rect.Left + 4, assetRect.Bottom + 2, item.Rect.Width - 8, 20 );
		var trackRect = new Rect( sliderRect.Left + 8, sliderRect.Center.y - 2, sliderRect.Width - 16, 4 );

		var localX = point.x - trackRect.Left;
		var weight = localX / trackRect.Width;
		return Math.Clamp( weight, 0.0f, 1.0f );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		if ( e.LeftMouseButton )
		{
			var item = GetItemAt( e.LocalPosition );
			if ( item != null && item.Object is Asset asset && IsPointInSliderArea( e.LocalPosition, item ) )
			{
				_isDraggingSlider = true;
				_draggedSliderAsset = asset;
				var weight = GetWeightFromSliderPosition( e.LocalPosition, item );
				SetAssetWeight( asset, weight );
				e.Accepted = true;
				return;
			}
		}

		base.OnMousePress( e );
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		if ( _isDraggingSlider && _draggedSliderAsset != null )
		{
			var item = GetItemAt( e.LocalPosition );
			if ( item != null && item.Object == _draggedSliderAsset )
			{
				var weight = GetWeightFromSliderPosition( e.LocalPosition, item );
				SetAssetWeight( _draggedSliderAsset, weight );
			}
			e.Accepted = true;
			return;
		}

		base.OnMouseMove( e );
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		if ( e.LeftMouseButton && _isDraggingSlider )
		{
			_isDraggingSlider = false;
			_draggedSliderAsset = null;
			e.Accepted = true;
			return;
		}

		base.OnMouseReleased( e );
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( LocalRect, 4 );

		base.OnPaint();
	}

	public void SetLayersList( ClutterLayersList layersList )
	{
		_layersList = layersList;
	}

	public void UpdateForSelectedLayer()
	{
		_currentLayerAssets.Clear();

		if ( _layersList != null )
		{
			var selectedLayers = _layersList.GetSelectedLayers();
			if ( selectedLayers.Count > 0 )
			{
				var selectedLayer = selectedLayers.First();
				// Convert ClutterObjects to Assets
				foreach ( var clutterObj in selectedLayer.Objects )
				{
					var asset = AssetSystem.FindByPath( clutterObj.Path );
					if ( asset != null )
					{
						_currentLayerAssets.Add( asset );
					}
				}
			}
		}

		BuildItems();
	}


	public override void OnDragHover( DragEvent ev )
	{
		base.OnDragHover( ev );

		// Accept assets from file browser
		foreach ( var dragAsset in ev.Data.Assets )
		{
			var path = dragAsset.AssetPath?.ToLower();
			if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
			{
				ev.Action = DropAction.Link;
				return;
			}
		}

		// Accept assets from object palette
		if ( ev.Data.Object is Asset asset )
		{
			var path = asset.Path?.ToLower();
			if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
			{
				ev.Action = DropAction.Copy;
				return;
			}
		}
	}

	public override void OnDragDrop( DragEvent ev )
	{
		base.OnDragDrop( ev );

		// Handle drops from file browser
		if ( ev.Data.Assets.Any() )
		{
			AddObjects( ev.Data.Assets );
		}
		// Handle drops from object palette
		else if ( ev.Data.Object is Asset asset )
		{
			AddAssetFromPalette( asset );
		}
	}



	private async void AddObjects( IEnumerable<DragAssetData> draggedAssets )
	{
		foreach ( var dragAsset in draggedAssets )
		{
			var asset = await dragAsset.GetAssetAsync();
			var path = asset.Path?.ToLower();

			if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
			{
				// Auto-add to palette if not present
				AddAssetToPaletteIfNeeded( asset );

				// Add to layer
				AddAssetFromPalette( asset );
			}
		}
	}

	private void AddAssetToPaletteIfNeeded( Asset asset )
	{
		// Find the object palette and add asset if not present
		var palette = FindObjectPalette();
		palette?.AddAssetToPaletteIfNotExists( asset );
	}

	private ObjectPalette FindObjectPalette()
	{
		// Search through parent hierarchy to find the palette
		Widget current = Parent;
		while ( current != null )
		{
			if ( current is ObjectPalette palette )
				return palette;

			// Search children
			foreach ( var child in current.Children )
			{
				if ( child is ObjectPalette childPalette )
					return childPalette;

				// Recursive search in child widgets
				var found = SearchForPalette( child );
				if ( found != null )
					return found;
			}

			current = current.Parent;
		}
		return null;
	}

	private ObjectPalette SearchForPalette( Widget widget )
	{
		if ( widget is ObjectPalette palette )
			return palette;

		foreach ( var child in widget.Children )
		{
			var found = SearchForPalette( child );
			if ( found != null )
				return found;
		}

		return null;
	}

	public void AddAssetFromPalette( Asset asset )
	{
		var path = asset.Path?.ToLower();
		if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
		{
			// Get the currently selected layer
			if ( _layersList != null )
			{
				var selectedLayers = _layersList.GetSelectedLayers();
				if ( selectedLayers.Count > 0 )
				{
					var selectedLayer = selectedLayers.First();

					// Use the actual asset path (not resource identifier)
					var assetPath = asset.Path;

					// Check if asset already exists
					if ( !selectedLayer.Objects.Any( obj => obj.Path == assetPath ) )
					{
						// For VMDL files, pre-load the model to ensure it's in memory
						if ( path?.EndsWith( ".vmdl" ) == true )
						{
							if ( asset.TryLoadResource<Model>( out var model ) )
							{
								// Loading via AssetSystem caches it in ResourceLibrary for engine access
								// Store the original asset path
								selectedLayer.Objects.Add( new ClutterObject( assetPath, 1.0f ) );
								Log.Info( $"Added and pre-loaded model: {assetPath} (ResourceName: {model.ResourceName})" );
							}
							else
							{
								Log.Warning( $"Failed to load model asset: {assetPath}" );
							}
						}
						else
						{
							// For prefabs, use the path as-is
							selectedLayer.Objects.Add( new ClutterObject( assetPath, 1.0f ) );
							Log.Info( $"Added clutter object with path: {assetPath}" );
						}

						UpdateForSelectedLayer();
					}
				}
			}
		}
	}

	private bool IsAssetMarkedAsSmall( Asset asset )
	{
		if ( _layersList != null )
		{
			var selectedLayers = _layersList.GetSelectedLayers();
			if ( selectedLayers.Count > 0 )
			{
				var selectedLayer = selectedLayers.First();
				var clutterObj = selectedLayer.Objects.FirstOrDefault( obj => obj.Path == asset.Path );
				return clutterObj.IsSmall;
			}
		}
		return false;
	}

	private void ToggleAssetSmallFlag( Asset asset )
	{
		if ( _layersList != null )
		{
			var selectedLayers = _layersList.GetSelectedLayers();
			if ( selectedLayers.Count > 0 )
			{
				var selectedLayer = selectedLayers.First();
				for ( int i = 0; i < selectedLayer.Objects.Count; i++ )
				{
					if ( selectedLayer.Objects[i].Path == asset.Path )
					{
						var clutterObj = selectedLayer.Objects[i];
						var newIsSmall = !clutterObj.IsSmall;
						clutterObj.IsSmall = newIsSmall;
						selectedLayer.Objects[i] = clutterObj;

						// Update existing instances with the new IsSmall flag
						UpdateExistingInstancesSmallFlag( asset.Path, newIsSmall, selectedLayer );

						Update();
						break;
					}
				}
			}
		}
	}

	private void UpdateExistingInstancesSmallFlag( string assetPath, bool isSmall, ClutterLayer layer )
	{
		// Update runtime instances
		if ( layer.Instances != null )
		{
			for ( int i = 0; i < layer.Instances.Count; i++ )
			{
				var instance = layer.Instances[i];

				// Check if this instance matches the asset path
				bool matches = false;
				if ( instance.ClutterType == Sandbox.ClutterInstance.Type.Model && instance.model != null )
				{
					matches = instance.model.ResourcePath == assetPath;
				}
				else if ( instance.ClutterType == Sandbox.ClutterInstance.Type.Prefab && instance.gameObject != null )
				{
					// For prefabs, we'd need to track the original path - skip for now
					continue;
				}

				if ( matches )
				{
					// Recreate the instance with the new IsSmall flag
					if ( instance.ClutterType == Sandbox.ClutterInstance.Type.Model )
					{
						layer.Instances[i] = new Sandbox.ClutterInstance( instance.model, instance.transform, isSmall );
					}
				}
			}
		}

		// Update the cell instances and rebuild
		// Get the component from the serialized object to access its scene
		var clutterComponent = _serializedObject?.Targets?.FirstOrDefault() as ClutterComponent;
		clutterComponent.SerializeToProperty();
	}

	private void ShowItemContext( object obj )
	{
		if ( obj is not Asset asset ) return;

		var m = new ContextMenu( this );
		m.AddOption( "Open In Editor", "edit", () => asset.OpenInEditor() );

		var isSmall = IsAssetMarkedAsSmall( asset );
		m.AddOption( isSmall ? "Unmark as Small" : "Mark as Small", "adjust", () =>
		{
			ToggleAssetSmallFlag( asset );
		} );

		m.AddSeparator();
		m.AddOption( "Remove", "delete", () =>
		{
			// Remove from selected layer
			if ( _layersList != null )
			{
				var selectedLayers = _layersList.GetSelectedLayers();
				if ( selectedLayers.Count > 0 )
				{
					var selectedLayer = selectedLayers.First();
					selectedLayer.Objects.RemoveAll( obj => obj.Path == asset.Path );
					UpdateForSelectedLayer();
				}
			}
		} );

		m.OpenAtCursor();
	}
}
