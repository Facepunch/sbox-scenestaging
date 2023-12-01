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

public class SculptPageWidget : Widget
{
	public SculptPageWidget( Widget parent ) : base( parent )
	{
		List<(string, Pixmap)> modes = new()
		{
			new( "Sculpt", Pixmap.FromFile( FileSystem.Content.GetFullPath( $"materials/tools/terrain/sculpt_raise_lower.png" ) ) ),
			new( "Smooth", Pixmap.FromFile(  FileSystem.Content.GetFullPath( $"materials/tools/terrain/sculpt_smooth.png" ) ) ),
			new( "Flatten", Pixmap.FromFile( FileSystem.Content.GetFullPath( $"materials/tools/terrain/sculpt_flatten.png" ) ) ),
			new( "Erode", Pixmap.FromFile( FileSystem.Content.GetFullPath($"materials/tools/terrain/sculpt_erode.png") ) ),
			new( "Noise", Pixmap.FromFile( FileSystem.Content.GetFullPath( $"materials/tools/terrain/sculpt_noise.png" ) ) )
		};

		ListView modeList = new();
		modeList.MaximumHeight = 64;
		modeList.SetItems( modes.Cast<object>() );
		modeList.ItemSize = new Vector2( 48, 48 );
		modeList.OnPaintOverride += () => PaintListBackground( modeList );
		modeList.ItemPaint = PaintModeItem;
		modeList.ItemAlign = Sandbox.UI.Align.SpaceBetween;
		modeList.ItemSpacing = new Vector2( 8, 8 );
		modeList.SelectItem( modes.First() );

		Layout = Layout.Column();
		Layout.Spacing = 8;
		Layout.Add( modeList );

		Layout.Add( new InfoBox( "Left Click to raise.\n\nHold Ctrl and Left Click to lower." ) );

		Layout.Add( new BrushSettingsWidget() );
	}

	private bool PaintListBackground( Widget widget )
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( widget.LocalRect );

		return false;
	}

	private void PaintModeItem( VirtualWidget widget )
	{
		var mode = ((string, Pixmap))widget.Object;

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

		Paint.Draw( widget.Rect.Shrink( 0, 0, 0, 16 ).Contain( mode.Item2.Size ), mode.Item2, widget.Selected ? 1.0f : 0.5f );

		Paint.SetPen( Theme.White.WithAlpha( widget.Selected ? 1.0f : 0.7f ) );
		Paint.DrawText( widget.Rect.Shrink( 0, 0, 0, 2 ), mode.Item1, TextFlag.CenterBottom );
	}

}
