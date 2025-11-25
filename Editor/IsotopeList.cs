using Sandbox;
using System;
namespace Editor;

/// <summary>
/// Grid-based list view for ClutterIsotope resources, similar to TerrainMaterialList.
/// </summary>
public class IsotopeList : ListView
{
	public ClutterIsotope SelectedIsotope { get; private set; }
	public Action<ClutterIsotope> OnIsotopeSelected { get; set; }

	public IsotopeList( Widget parent ) : base( parent )
	{
		ItemSelected = OnItemClicked;
		ItemActivated = OnItemDoubleClicked;
		ItemContextMenu = ShowItemContext;
		Margin = 8;
		ItemSpacing = 4;
		MinimumHeight = 200;
		
		ItemSize = new Vector2( 86, 86 + 16 );
		ItemAlign = Sandbox.UI.Align.FlexStart;

		BuildItems();
	}

	protected void OnItemClicked( object value )
	{
		if ( value is not ClutterIsotope isotope )
			return;

		SelectedIsotope = isotope;
		OnIsotopeSelected?.Invoke( isotope );
	}

	protected void OnItemDoubleClicked( object obj )
	{
		if ( obj is not ClutterIsotope isotope )
			return;

		var asset = AssetSystem.FindByPath( isotope.ResourcePath );
		asset?.OpenInEditor();
	}

	private void ShowItemContext( object obj )
	{
		if ( obj is not ClutterIsotope isotope )
			return;

		var m = new ContextMenu( this );
		m.AddOption( "Open In Editor", "edit", () =>
		{
			var asset = AssetSystem.FindByPath( isotope.ResourcePath );
			asset?.OpenInEditor();
		} );

		m.AddOption( "Show In Asset Browser", "folder_open", () =>
		{
			var asset = AssetSystem.FindByPath( isotope.ResourcePath );
			if ( asset != null )
			{
				MainAssetBrowser.Instance?.Local.FocusOnAsset( asset );
			}
		} );

		m.OpenAtCursor();
	}

	public void BuildItems()
	{
		var isotopes = ResourceLibrary.GetAll<ClutterIsotope>().ToList();
		SetItems( isotopes.Cast<object>() );
	}

	public void Refresh()
	{
		BuildItems();
	}

	protected override void PaintItem( VirtualWidget item )
	{
		var rect = item.Rect.Shrink( 0, 0, 0, 16 );

		if ( item.Object is not ClutterIsotope isotope )
			return;

		var asset = AssetSystem.FindByPath( isotope.ResourcePath );

		if ( asset is null )
		{
			Paint.SetDefaultFont();
			Paint.SetPen( Color.Red );
			Paint.DrawText( item.Rect.Shrink( 2 ), "<ERROR>", TextFlag.Center );
			return;
		}

		// Selection/hover highlight
		var isSelected = isotope == SelectedIsotope || item.Selected;
		if ( isSelected || Paint.HasMouseOver )
		{
			Paint.SetBrush( Theme.Blue.WithAlpha( isSelected ? 0.5f : 0.2f ) );
			Paint.ClearPen();
			Paint.DrawRect( item.Rect, 4 );
		}

		// Thumbnail
		var pixmap = asset.GetAssetThumb();
		if ( pixmap != null )
		{
			Paint.Draw( rect.Shrink( 2 ), pixmap );
		}
		else
		{
			// Fallback: draw background and icon
			Paint.SetBrush( Theme.ControlBackground );
			Paint.ClearPen();
			Paint.DrawRect( rect.Shrink( 2 ), 4 );

			Paint.SetPen( Theme.Green );
			Paint.DrawIcon( rect.Shrink( 12 ), "forest", 32 );
		}

		// Entry count badge
		var entryCount = isotope.Entries?.Count ?? 0;
		if ( entryCount > 0 )
		{
			var badgeRect = new Rect( rect.Right - 18, rect.Top + 4, 16, 16 );
			Paint.SetBrush( Theme.ControlBackground.WithAlpha( 0.8f ) );
			Paint.ClearPen();
			Paint.DrawRect( badgeRect, 8 );

			Paint.SetDefaultFont( 9 );
			Paint.SetPen( Color.White );
			Paint.DrawText( badgeRect, entryCount.ToString(), TextFlag.Center );
		}

		// Name label
		Paint.SetDefaultFont();
		Paint.SetPen( Theme.Text );
		Paint.DrawText( item.Rect.Shrink( 2 ), isotope.ResourceName, TextFlag.CenterBottom );
	}

	protected override void OnPaint()
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( LocalRect, 4 );

		base.OnPaint();
	}
}
