using Sandbox;

namespace Editor;

/// <summary>
/// Represent a collection of objects which can be used by clutter layers
/// </summary>
public class ObjectPalette : ListView
{
	protected List<Asset> PaletteAssets = new();
	private Scene _scene;

	private ClutterObjectsList _targetObjectsList;

	public ObjectPalette( Widget parent, Scene scene ) : base( parent )
	{
		_scene = scene;
		Margin = 8;
		ItemSpacing = 4;
		AcceptDrops = true;
		MinimumHeight = 120;
		MultiSelect = true;

		ItemSize = new Vector2( 68, 68 + 16 );
		ItemAlign = Sandbox.UI.Align.FlexStart;

		ItemActivated = OnItemDoubleClicked;

		PaletteAssets.Clear();

		// Populate palette from all assets in all layers from ClutterSystem
		var clutterSystem = _scene?.GetSystem<ClutterSystem>();
		if ( clutterSystem != null )
		{
			foreach ( var layer in clutterSystem.GetAllLayers() )
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

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		var selectedAsset = GetSelectedAsset();
		if ( selectedAsset != null && _targetObjectsList != null )
		{
			var menu = new ContextMenu( this );
			menu.AddOption( "Add to Layer", "add", () =>
			{
				_targetObjectsList.AddAssetFromPalette( selectedAsset );
				Log.Info( $"Added {selectedAsset.Name} to layer objects" );
			} );
			menu.OpenAt( e.LocalPosition );
		}
	}

	internal void SetTargetObjectsList( ClutterObjectsList targetList )
	{
		_targetObjectsList = targetList;
	}

	protected override bool OnDragItem( VirtualWidget item )
	{
		if ( item.Object is not Asset asset )
			return false;

		// Create Drag object
		var drag = new Drag( this );
		drag.Data.Text = asset.Path;
		drag.Data.Url = new System.Uri( "file:///" + asset.AbsolutePath );
		drag.Data.Object = asset;

		// Execute the drag operation
		drag.Execute();

		return true;
	}

	private void OnItemDoubleClicked( object item )
	{
		if ( item is Asset asset && _targetObjectsList != null )
		{
			_targetObjectsList.AddAssetFromPalette( asset );
			Log.Info( $"Added {asset.Name} to layer objects (double-click)" );
		}
	}

	public override void OnDragHover( DragEvent ev )
	{
		base.OnDragHover( ev );

		// Accept prefabs and models
		foreach ( var dragAsset in ev.Data.Assets )
		{
			var path = dragAsset.AssetPath?.ToLower();
			if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
			{
				ev.Action = DropAction.Link;
				return;
			}
		}
	}

	public override void OnDragDrop( DragEvent ev )
	{
		base.OnDragDrop( ev );
		AddAssetsToPalette( ev.Data.Assets );
	}

	private async void AddAssetsToPalette( IEnumerable<DragAssetData> draggedAssets )
	{
		foreach ( var dragAsset in draggedAssets )
		{
			var asset = await dragAsset.GetAssetAsync();
			var path = asset.Path?.ToLower();

			if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
			{
				if ( !PaletteAssets.Contains( asset ) )
				{
					PaletteAssets.Add( asset );
				}
			}
		}

		SetItems( PaletteAssets.Cast<object>().ToList() );
	}

	protected override void PaintItem( VirtualWidget item )
	{
		var rect = item.Rect.Shrink( 0, 0, 0, 16 );

		if ( item.Object is not Asset asset )
			return;

		if ( item.Selected || Paint.HasMouseOver )
		{
			Paint.SetBrush( Theme.Blue.WithAlpha( item.Selected ? 0.5f : 0.2f ) );
			Paint.ClearPen();
			Paint.DrawRect( item.Rect, 4 );
		}

		var pixmap = asset.GetAssetThumb();
		Paint.Draw( rect.Shrink( 2 ), pixmap );

		// Draw type icon
		var path = asset.Path?.ToLower();
		var iconRect = new Rect( item.Rect.Right - 18, item.Rect.Top + 2, 16, 16 );
		Paint.SetPen( Color.White.WithAlpha( 0.8f ), 1 );

		if ( path?.EndsWith( ".prefab" ) == true )
		{
			Paint.DrawIcon( iconRect, "view_in_ar", 12 );
		}
		else if ( path?.EndsWith( ".vmdl" ) == true )
		{
			Paint.DrawIcon( iconRect, "category", 12 );
		}

		Paint.SetDefaultFont();
		Paint.SetPen( Theme.Text );
		Paint.DrawText( item.Rect.Shrink( 2 ), asset.Name, TextFlag.CenterBottom );
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.Darken( 0.05f ) );
		Paint.DrawRect( LocalRect, 4 );

		// Draw drop zone hint when empty
		if ( PaletteAssets.Count == 0 )
		{
			Paint.SetDefaultFont();
			Paint.SetPen( Theme.Text.WithAlpha( 0.5f ) );
			Paint.DrawText( LocalRect, "Drag assets here to create palette", TextFlag.Center );
		}

		base.OnPaint();
	}

	public Asset GetSelectedAsset()
	{
		return Selection.Count > 0 && Selection.First() is Asset asset ? asset : null;
	}

	public void AddAssetToPaletteIfNotExists( Asset asset )
	{
		if ( asset == null ) return;

		var path = asset.Path?.ToLower();
		if ( path?.EndsWith( ".prefab" ) == true || path?.EndsWith( ".vmdl" ) == true )
		{
			if ( !PaletteAssets.Contains( asset ) )
			{
				PaletteAssets.Add( asset );
				SetItems( PaletteAssets.Cast<object>().ToList() );
			}
		}
	}
}
