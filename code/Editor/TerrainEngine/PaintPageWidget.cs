using Sandbox.UI;

namespace Editor.TerrainEngine;

file class InfoBox : Widget
{
	public Label Label;

	public InfoBox( string title, Widget parent = null ) : base( parent )
	{
		Layout = Layout.Column();

		Label = new Label( title, this );
		Label.WordWrap = true;
		Label.Alignment = TextFlag.LeftTop;

		Layout.Add( Label );

		Layout.Margin = new Margin( 16, 16, 16, 16 );

		Label.Color = Color.White.WithAlpha( 0.85f );
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.SetPen( Theme.Black.WithAlpha( 0.4f ), 2 );
		Paint.SetBrushRadial( new Vector2( 64, 0 ), 256, Theme.Blue.WithAlpha( 0.08f ), Theme.Blue.WithAlpha( 0.03f ) );
		Paint.DrawRect( LocalRect, 0 );
	}

}

public class PaintPageWidget : Widget
{
	public PaintPageWidget( Widget parent ) : base( parent )
	{
		// stubbing
		List<(int, string)> layers = new()
		{
			new( 0, "smile" ),
			new( 1, "smile" ),
			new( 2, "smile" ),
			new( 3, "smile" ),
		};

		ListView layersList = new();
		layersList.MinimumHeight = 240;
		layersList.SetItems( layers.Cast<object>() );
		layersList.ItemSize = new Vector2( -1, 48 );
		layersList.OnPaintOverride += () => PaintListBackground( layersList );
		layersList.ItemPaint = PaintLayerItem;
		layersList.ItemAlign = Sandbox.UI.Align.SpaceBetween;
		layersList.ItemSpacing = new Vector2( 8, 8 );
		layersList.SelectItem( layers.First() );

		Layout = Layout.Column();
		Layout.Spacing = 8;

		var label = new Label( "Layers" );
		label.SetStyles( "font-weight: bold" );
		Layout.Add( label );

		Layout.Add( layersList );

		Layout.Add( new InfoBox( "Left Click to paint." ) );

		Layout.Add( new BrushSettingsWidget() );
	}

	private bool PaintListBackground( Widget widget )
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( widget.LocalRect );

		return false;
	}

	private void PaintLayerItem( VirtualWidget widget )
	{
		var mode = ((int, string))widget.Object;

		Paint.ClearPen();
		Paint.ClearBrush();

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		if ( widget.Hovered || widget.Selected )
		{
			Paint.ClearPen();
			Paint.SetBrush( widget.Selected ? Theme.Primary : Color.White.WithAlpha( 0.1f ) );
			Paint.DrawRect( widget.Rect.Grow( 2 ), 3 );
		}


		var iconRect = widget.Rect.Align( new Vector2( widget.Rect.Height ), TextFlag.LeftCenter );
		Paint.SetPen( Theme.White.WithAlpha( widget.Selected ? 1.0f : 0.7f ) );
		Paint.DrawIcon( iconRect, "layers", 24 );

		Paint.SetPen( Theme.White.WithAlpha( widget.Selected ? 1.0f : 0.7f ) );
		Paint.DrawText( widget.Rect.Shrink( 48, 0 ), $"Layer {mode.Item1}", TextFlag.LeftCenter );
	}
}
