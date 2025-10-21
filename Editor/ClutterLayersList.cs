using Sandbox;
using System;
using static Sandbox.ClutterInstance;

namespace Editor;

/// <summary>
/// Represents a list of clutter layers in the editor UI
/// </summary>
public class ClutterLayersList : ListView
{
	private SerializedProperty _layersProperty;
	private SerializedCollection _collection;
	private List<ClutterLayer> _selectedLayers = [];
	private SerializedObject _serializedObject;
	private ClutterObjectsList _objectsList;
	private bool _isScatterBrushMode;

	public ClutterLayersList( Widget parent, SerializedObject serializedObject ) : base( parent )
	{
		_serializedObject = serializedObject;

		// Check if this is a ScatterBrush or ClutterComponent
		var firstTarget = serializedObject.Targets.FirstOrDefault();
		_isScatterBrushMode = firstTarget is ScatterBrush;

		// Get the Layers property (both types have it)
		_layersProperty = serializedObject.GetProperty( "Layers" );

		if ( _layersProperty.TryGetAsObject( out var obj ) && obj is SerializedCollection sc )
		{
			_collection = sc;
			_collection.OnEntryAdded = BuildItems;
			_collection.OnEntryRemoved = BuildItems;
		}

		ItemContextMenu = ShowItemContext;
		ItemSelected = OnItemSelected;
		ItemActivated = OnItemDoubleClicked;
		ItemClicked = OnItemClicked;
		Margin = 8;
		ItemSpacing = 2;
		MinimumHeight = 200;
		MultiSelect = true;
		AcceptDrops = true;

		ItemSize = new Vector2( 0, 24 );
		ItemAlign = Sandbox.UI.Align.FlexStart;

		BuildItems();
	}

	protected void OnItemSelected( object value )
	{
		UpdateSelectedLayers();

		NotifyObjectsListUpdate();
	}

	private void NotifyObjectsListUpdate()
	{
		_objectsList?.UpdateForSelectedLayer();
	}


	public void SetObjectsList( ClutterObjectsList objectsList )
	{
		_objectsList = objectsList;
	}

	protected void OnItemClicked( object value )
	{
		// This will be handled in OnMousePress instead
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		// Let ListView handle normal clicks
		base.OnMouseClick( e );
	}

	private bool IsPointInRect( Vector2 point, Rect rect )
	{
		return point.x >= rect.Left && point.x <= rect.Right &&
			   point.y >= rect.Top && point.y <= rect.Bottom;
	}

	private void UpdateSelectedLayers()
	{
		_selectedLayers.Clear();

		foreach ( var selectedItem in Selection )
		{
			if ( selectedItem is ClutterLayer layer )
			{
				_selectedLayers.Add( layer );
			}
		}
	}

	public List<ClutterLayer> GetSelectedLayers()
	{
		UpdateSelectedLayers();
		return _selectedLayers;
	}

	public int GetFirstSelectedIndex()
	{
		if ( Selection.Count == 0 ) return -1;

		var items = GetItems();
		for ( int i = 0; i < items.Count; i++ )
		{
			if ( Selection.Contains( items[i] ) )
				return i;
		}
		return -1;
	}

	public void SelectLastItem()
	{
		var items = GetItems();
		if ( items.Count > 0 )
		{
			Selection.Set( items.Last() );
		}
		else
		{
			Selection.Clear();
		}
	}

	public void SelectLayerByInstance( ClutterLayer targetLayer )
	{
		var items = GetItems();
		foreach ( var item in items )
		{
			if ( item is ClutterLayer layer && layer == targetLayer )
			{
				Selection.Set( layer );
				NotifyObjectsListUpdate();
				return;
			}
		}
	}

	public void SelectNextAfterDeletion( int deletedIndex )
	{
		if ( deletedIndex < 0 ) return;

		var items = GetItems();
		if ( items.Count == 0 ) return;

		// Select the previous item, or the last item if deleting the first
		var indexToSelect = deletedIndex > 0 ? deletedIndex - 1 : Math.Min( deletedIndex, items.Count - 1 );
		Selection.Set( items[indexToSelect] );
	}

	protected void OnItemDoubleClicked( object obj )
	{
		if ( obj is not ClutterLayer layer ) return;
		// Could open layer settings here
		Log.Info( $"Double clicked layer: {layer.Name}" );
	}

	private void ShowItemContext( object obj )
	{
		if ( obj is not ClutterLayer layer || _collection == null ) return;

		var m = new ContextMenu( this );
		m.AddOption( "Rename", "edit", () => RenameLayer( layer ) );
		m.AddOption( "Duplicate", "content_copy", () => DuplicateLayer( layer ) );
		m.AddSeparator();

		// Only show Purge Instances for ClutterComponent (not ScatterBrush)
		if ( !_isScatterBrushMode )
		{
			m.AddOption( "Purge Instances", "delete_sweep", () => PurgeLayer( layer ) );
		}

		m.AddOption( "Remove", "delete", () => RemoveLayer( layer ) );

		m.OpenAtCursor();
	}

	private void RenameLayer( ClutterLayer layer )
	{
		var oldName = layer.Name;
		Dialog.AskString(
			newName =>
			{
				// Update the layer name
				layer.Name = newName;

				// Find and update the corresponding GameObject name (only for ClutterComponent)
				if ( !_isScatterBrushMode )
				{
					var clutterComponent = _serializedObject.Targets.FirstOrDefault() as ClutterComponent;
					if ( clutterComponent != null )
					{
						foreach ( var child in clutterComponent.GameObject.Children )
						{
							if ( child.Name == oldName && child.Tags.Contains( "clutter_layer" ) )
							{
								child.Name = newName;
								break;
							}
						}
					}
				}

				BuildItems();
			},
			"Enter new layer name:",
			"Rename",
			"Cancel",
			layer.Name,
			"Rename Layer"
		);
	}

	private void DuplicateLayer( ClutterLayer layer )
	{
		if ( _collection == null ) return;

		var newLayer = new ClutterLayer()
		{
			Name = $"{layer.Name} Copy",
			Objects = [.. layer.Objects]
		};

		_collection.Add( newLayer );
	}

	private void PurgeLayer( ClutterLayer layer )
	{
		// Only available for ClutterComponent (ScatterBrush doesn't have runtime instances)
		if ( _isScatterBrushMode )
		{
			Log.Warning( "Cannot purge instances from ScatterBrush (use this feature in-scene)" );
			return;
		}

		// Clear all instances from the layer
		if ( layer.Instances != null && layer.Instances.Count > 0 )
		{
			var clutterComponent = _serializedObject.Targets.FirstOrDefault() as ClutterComponent;
			if ( clutterComponent?.Scene != null )
			{
				var clutterSystem = clutterComponent.Scene.GetSystem<ClutterSystem>();

				// Unregister all instances from the system
				foreach ( var instance in layer.Instances.ToList() )
				{
					clutterSystem?.UnregisterClutter( instance );
					ClutterSystem.DestroyInstance( instance );
				}

				// Clear the instances list
				layer.Instances.Clear();

				// Serialization now happens automatically through ClutterSystem metadata

				Log.Info( $"Purged all instances from layer '{layer.Name}'" );
				BuildItems();
			}
		}
	}

	private void RemoveLayer( ClutterLayer layer )
	{
		if ( _collection == null ) return;

		int index = 0;
		foreach ( var entry in _collection )
		{
			var existingLayer = entry.GetValue<ClutterLayer>();
			if ( existingLayer == layer )
			{
				_collection.RemoveAt( index );
				break;
			}
			index++;
		}
	}

	public void BuildItems()
	{
		var items = GetItems();
		SetItems( items );
	}

	private List<object> GetItems()
	{
		var items = new List<object>();
		if ( _collection != null )
		{
			foreach ( var entry in _collection )
			{
				var layer = entry.GetValue<ClutterLayer>();
				if ( layer != null )
				{
					items.Add( layer );
				}
			}
		}
		return items;
	}

	protected override void PaintItem( VirtualWidget item )
	{
		if ( item.Object is not ClutterLayer layer )
			return;

		var rect = item.Rect;

		// Background
		if ( item.Selected || Paint.HasMouseOver )
		{
			Paint.SetBrush( Theme.Blue.WithAlpha( item.Selected ? 0.3f : 0.1f ) );
			Paint.ClearPen();
			Paint.DrawRect( rect, 2 );
		}

		// Layer name with instance count
		var textRect = new Rect( rect.Left + 8, rect.Top, rect.Right - 24, rect.Height );
		Paint.SetDefaultFont();
		Paint.SetPen( Theme.Text );

		var instanceCount = layer.Instances?.Count ?? 0;
		var displayText = instanceCount > 0 ? $"{layer.Name} ({instanceCount})" : layer.Name;
		Paint.DrawText( textRect, displayText, TextFlag.LeftCenter );

	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.Darken( 0.1f ) );
		Paint.DrawRect( LocalRect, 4 );

		base.OnPaint();
	}
}
