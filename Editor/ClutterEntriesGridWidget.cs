using Sandbox.Clutter;
using Sandbox.UI;
using System;

namespace Editor;

/// <summary>
/// Custom control widget for editing ClutterEntry list in a grid layout.
/// Shows entries in a visual grid similar to ClutterList.
/// Supports drag & drop of Prefabs and Models to add entries.
/// </summary>
[CustomEditor( typeof( List<ClutterEntry> ), NamedEditor = "ClutterEntriesGrid" )]
public class ClutterEntriesGridWidget : ControlWidget
{
	private SerializedProperty _listProperty;
	private ClutterListView _listView;

	public override bool SupportsMultiEdit => false;

	public ClutterEntriesGridWidget( SerializedProperty property ) : base( property )
	{
		_listProperty = property;

		Layout = Layout.Column();
		Layout.Spacing = 0;
		
		// Enable drag & drop on the parent widget
		AcceptDrops = true;

		_listView = new ClutterListView( this, _listProperty );
		_listView.OnRebuildNeeded = () => RebuildList();
		Layout.Add( _listView );
	}

	private void RebuildList()
	{
		_listView?.BuildItems();
	}

	// Handle drops on the parent widget (empty areas)
	public override void OnDragHover( DragEvent e )
	{
		base.OnDragHover( e );

		if ( e.Data.Assets == null ) return;

		foreach ( var dragAsset in e.Data.Assets )
		{
			var path = dragAsset.AssetPath;
			if ( string.IsNullOrEmpty( path ) ) continue;

			var isModel = path.EndsWith( ".vmdl" ) || path.EndsWith( ".vmdl_c" );
			var isPrefab = path.EndsWith( ".prefab" );

			if ( isModel || isPrefab )
			{
				e.Action = DropAction.Copy;
				return;
			}
		}
	}

	public override void OnDragDrop( DragEvent e )
	{
		base.OnDragDrop( e );
		if ( e.Data.Assets != null )
			AddAssetsFromDrop( e.Data.Assets );
	}

	internal async void AddAssetsFromDrop( IEnumerable<DragAssetData> draggedAssets )
	{
		foreach ( var dragAsset in draggedAssets )
		{
			var path = dragAsset.AssetPath;
			if ( string.IsNullOrEmpty( path ) ) continue;

			var isModel = path.EndsWith( ".vmdl" ) || path.EndsWith( ".vmdl_c" );
			var isPrefab = path.EndsWith( ".prefab" );

			if ( !isModel && !isPrefab ) continue;

			var asset = await dragAsset.GetAssetAsync();
			if ( asset == null ) continue;

			// Get or create the list
			var entries = _listProperty.GetValue<IList<ClutterEntry>>();
			if ( entries == null )
			{
				entries = new List<ClutterEntry>();
			}

			// Create new entry
			var newEntry = new ClutterEntry();

			if ( isModel )
			{
				if ( asset.TryLoadResource<Model>( out var model ) )
				{
					newEntry.Model = model;
				}
			}
			else if ( isPrefab )
			{
				if ( asset.TryLoadResource<PrefabFile>( out var prefabFile ) )
				{
					var prefab = SceneUtility.GetPrefabScene( prefabFile );
					newEntry.Prefab = prefab;
				}
			}

			// Add to list and notify property system
			entries.Add( newEntry );
			_listProperty.SetValue( entries );
			_listProperty.Parent?.NoteChanged( _listProperty );
		}

		RebuildList();
	}

	/// <summary>
	/// ListView for displaying clutter entries in a grid
	/// </summary>
	private class ClutterListView : ListView
	{
		private SerializedProperty _listProperty;
		public Action OnRebuildNeeded;
		private int _dragOverIndex = -1;

		public ClutterListView( Widget parent, SerializedProperty listProperty ) : base( parent )
		{
			_listProperty = listProperty;

			ItemSpacing = 4;
			Margin = 8;
			MinimumHeight = 90;
			ItemSize = new Vector2( 86, 86 + 16 );
			ItemAlign = Align.FlexStart;
			ItemContextMenu = ShowItemContext;
			AcceptDrops = true;

			BuildItems();
		}

		protected override bool OnDragItem( VirtualWidget item )
		{
			if ( item.Object is not ClutterEntry entry ) return false;

			var entries = _listProperty.GetValue<List<ClutterEntry>>();
			if ( entries == null ) return false;

			var index = entries.IndexOf( entry );
			if ( index < 0 ) return false;

			var drag = new Drag( this );
			drag.Data.Object = index;
			drag.Execute();
			return true;
		}

		protected override DropAction OnItemDrag( ItemDragEvent e )
		{
			_dragOverIndex = -1;

			// Handle reordering visual feedback
			if ( e.Data.Object is int && e.Item.Object is ClutterEntry )
			{
				// Find target index for highlight
				var idx = 0;
				foreach ( var item in Items )
				{
					if ( item == e.Item.Object )
					{
						_dragOverIndex = idx;
						break;
					}
					idx++;
				}
				Update();
				return DropAction.Move;
			}

			// Handle external asset drops
			if ( e.Data.Assets != null )
			{
				foreach ( var dragAsset in e.Data.Assets )
				{
					var path = dragAsset.AssetPath;
					if ( string.IsNullOrEmpty( path ) ) continue;

					if ( path.EndsWith( ".vmdl" ) || path.EndsWith( ".vmdl_c" ) || path.EndsWith( ".prefab" ) )
						return DropAction.Copy;
				}
			}

			return base.OnItemDrag( e );
		}

		public override void OnDragLeave()
		{
			base.OnDragLeave();
			_dragOverIndex = -1;
			Update();
		}

		public void BuildItems()
		{
			var entries = _listProperty.GetValue<IList<ClutterEntry>>();
			if ( entries != null && entries.Count > 0 )
			{
				SetItems( entries.Cast<object>() );
			}
			else
			{
				SetItems( Enumerable.Empty<object>() );
			}
		}

		private void ShowItemContext( object obj )
		{
			if ( obj is not ClutterEntry entry )
				return;

			var entries = _listProperty.GetValue<IList<ClutterEntry>>();
			if ( entries == null ) return;

			var index = entries.IndexOf( entry );
			if ( index < 0 ) return;

			var menu = new Menu( this );

			menu.AddOption( "Set Weight...", "balance", () =>
			{
				var popup = new PopupWidget( this );
				popup.Layout = Layout.Row();
				popup.Layout.Margin = 8;
				popup.Layout.Spacing = 8;
				popup.MinimumWidth = 200;

				var slider = new FloatSlider( popup );
				slider.Minimum = 0.01f;
				slider.Maximum = 1f;
				slider.Value = entry.Weight;
				slider.MinimumWidth = 150;
				slider.OnValueEdited += () =>
				{
					entry.Weight = slider.Value;
					_listProperty.SetValue( entries );
					_listProperty.Parent?.NoteChanged( _listProperty );
					Update();
				};
				popup.Layout.Add( slider );

				popup.OpenAtCursor();
			} );

			menu.AddSeparator();

			menu.AddOption( "Remove Entry", "close", () =>
			{
				entries.RemoveAt( index );
				_listProperty.SetValue( entries );
				_listProperty.Parent?.NoteChanged( _listProperty );
				OnRebuildNeeded?.Invoke();
			} );

			menu.OpenAtCursor();
		}

		// Handle drops on items (to replace their asset)
		public override void OnDragDrop( DragEvent e )
		{
			_dragOverIndex = -1;

			// Handle internal reordering
			if ( e.Data.Object is int oldIndex )
			{
				var hoveredItem = GetItemAt( e.LocalPosition );
				if ( hoveredItem?.Object is ClutterEntry )
				{
					// Find the target index
					var newIndex = -1;
					var idx = 0;
					foreach ( var item in Items )
					{
						if ( item == hoveredItem.Object )
						{
							newIndex = idx;
							break;
						}
						idx++;
					}

					var entries = _listProperty.GetValue<List<ClutterEntry>>();
					if ( entries != null && oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex && oldIndex < entries.Count && newIndex < entries.Count )
					{
						// Swap positions
						(entries[oldIndex], entries[newIndex]) = (entries[newIndex], entries[oldIndex]);

						_listProperty.SetValue( entries );
						_listProperty.Parent?.NoteChanged( _listProperty );
						BuildItems();
					}
				}
				Update();
				return;
			}

			// Find which item was dropped on
			if ( e.Data.Assets == null ) return;

			var targetItem = GetItemAt( e.LocalPosition );
			if ( targetItem?.Object is ClutterEntry entry )
			{
				// Dropped on an existing item - replace its asset
				ReplaceEntryAsset( entry, e.Data.Assets );
			}
			else
			{
				// Dropped on empty space - add new entry via parent
				if ( Parent is ClutterEntriesGridWidget parent )
				{
					parent.AddAssetsFromDrop( e.Data.Assets );
				}
			}
		}

		private async void ReplaceEntryAsset( ClutterEntry entry, IEnumerable<DragAssetData> draggedAssets )
		{
			foreach ( var dragAsset in draggedAssets )
			{
				var path = dragAsset.AssetPath;
				if ( string.IsNullOrEmpty( path ) ) continue;

				var isModel = path.EndsWith( ".vmdl" ) || path.EndsWith( ".vmdl_c" );
				var isPrefab = path.EndsWith( ".prefab" );

				if ( !isModel && !isPrefab ) continue;

				var asset = await dragAsset.GetAssetAsync();
				if ( asset == null ) continue;

				// Replace the entry's asset
				if ( isModel )
				{
					if ( asset.TryLoadResource<Model>( out var model ) )
					{
						entry.Model = model;
						entry.Prefab = null;
					}
				}
				else if ( isPrefab )
				{
					if ( asset.TryLoadResource<PrefabFile>( out var prefabFile ) )
					{
						var prefab = SceneUtility.GetPrefabScene( prefabFile );
						entry.Prefab = prefab;
						entry.Model = null;
					}
				}

				var entries = _listProperty.GetValue<IList<ClutterEntry>>();
				_listProperty.SetValue( entries );
				_listProperty.Parent?.NoteChanged( _listProperty );

				OnRebuildNeeded?.Invoke();
				return; // Only replace with first valid asset
			}
		}

		public override void OnDragHover( DragEvent e )
		{
			base.OnDragHover( e );

			// Accept internal reordering
			if ( e.Data.Object is int )
			{
				e.Action = DropAction.Move;
				return;
			}

			if ( e.Data.Assets == null ) return;

			foreach ( var dragAsset in e.Data.Assets )
			{
				var path = dragAsset.AssetPath;
				if ( string.IsNullOrEmpty( path ) ) continue;

				var isModel = path.EndsWith( ".vmdl" ) || path.EndsWith( ".vmdl_c" );
				var isPrefab = path.EndsWith( ".prefab" );

				if ( isModel || isPrefab )
				{
					e.Action = DropAction.Copy;
					return;
				}
			}
		}

		protected override void PaintItem( VirtualWidget item )
		{
			if ( item.Object is not ClutterEntry entry )
				return;

			var rect = item.Rect.Shrink( 0, 0, 0, 16 ); // Reserve space for weight label

			// Get asset for thumbnail
			Asset asset = null;
			if ( entry.Prefab != null )
			{
				asset = AssetSystem.All.FirstOrDefault( a =>
					a.Path.EndsWith( ".prefab" ) &&
					a.Name == entry.Prefab.Name
				);
			}
			else if ( entry.Model != null )
			{
				asset = AssetSystem.FindByPath( entry.Model.ResourcePath );
			}

			// Hover highlight
			if ( Paint.HasMouseOver )
			{
				Paint.SetBrush( Theme.Blue.WithAlpha( 0.2f ) );
				Paint.ClearPen();
				Paint.DrawRect( item.Rect, 4 );
			}

			// Drag-over highlight
			var entries = _listProperty.GetValue<IList<ClutterEntry>>();
			if ( entries != null && _dragOverIndex >= 0 && _dragOverIndex < entries.Count )
			{
				if ( entries[_dragOverIndex] == entry )
				{
					Paint.ClearBrush();
					Paint.SetPen( Theme.Primary, 2f );
					Paint.DrawRect( item.Rect.Shrink( 1 ), 4 );
				}
			}

			// Background
			Paint.SetBrush( Theme.ControlBackground );
			Paint.ClearPen();
			Paint.DrawRect( rect.Shrink( 2 ), 4 );

			// Draw thumbnail
			if ( asset != null )
			{
				var pixmap = asset.GetAssetThumb( true );
				if ( pixmap != null )
				{
					Paint.Draw( rect.Shrink( 2 ), pixmap );
				}
				else
				{
					// No preview available
					Paint.SetPen( Theme.Text.WithAlpha( 0.3f ) );
					Paint.DrawIcon( rect.Shrink( 24 ), "category", 32 );
				}

				// Asset name overlay
				var nameRect = new Rect( rect.Left + 2, rect.Bottom - 20, rect.Width - 4, 18 );
				Paint.SetBrush( Color.Black.WithAlpha( 0.7f ) );
				Paint.ClearPen();
				Paint.DrawRect( nameRect, 2 );

				Paint.SetDefaultFont( 8 );
				Paint.SetPen( Color.White );
				Paint.DrawText( nameRect, asset.Name, TextFlag.Center );
			}
			else
			{
				// Empty entry
				Paint.SetPen( Theme.Text.WithAlpha( 0.3f ) );
				Paint.DrawIcon( rect.Shrink( 24 ), "add_photo_alternate", 32 );

				Paint.SetDefaultFont( 9 );
				Paint.SetPen( Theme.Text.WithAlpha( 0.5f ) );
				Paint.DrawText( rect.Shrink( 4 ), "Empty\nEntry", TextFlag.Center );
			}

			// Border (clear brush to avoid filling over thumbnail)
			Paint.ClearBrush();
			Paint.SetPen( Theme.ControlBackground.Lighten( 0.1f ) );
			Paint.DrawRect( rect.Shrink( 2 ), 4 );

			// Index in top left
			var itemIndex = entries?.IndexOf( entry ) ?? -1;
			if ( itemIndex >= 0 )
			{
				var indexRect = new Rect( rect.Left + 4, rect.Top + 4, 18, 14 );
				Paint.SetBrush( Color.Black.WithAlpha( 0.6f ) );
				Paint.ClearPen();
				Paint.DrawRect( indexRect, 2 );

				Paint.SetDefaultFont( 8 );
				Paint.SetPen( Color.White );
				Paint.DrawText( indexRect, $"{itemIndex}", TextFlag.Center );
			}

			// Weight label at bottom
			Paint.SetDefaultFont( 9 );
			Paint.SetPen( Theme.Text.WithAlpha( 0.7f ) );
			Paint.DrawText( item.Rect.Shrink( 2 ), $"Weight: {entry.Weight:F2}", TextFlag.CenterBottom );
		}

		protected override void OnPaint()
		{
			// Background
			Paint.ClearPen();
			Paint.SetBrush( Theme.ControlBackground );
			Paint.DrawRect( LocalRect, 4 );

			// Show hint text when empty
			var entries = _listProperty.GetValue<IList<ClutterEntry>>();
			if ( entries == null || entries.Count == 0 )
			{
				Paint.SetDefaultFont( 11 );
				Paint.SetPen( Theme.Text.WithAlpha( 0.4f ) );
				Paint.DrawText( LocalRect, "Drag & drop Prefabs or Models here", TextFlag.Center );
			}

			base.OnPaint();
		}
	}
}
