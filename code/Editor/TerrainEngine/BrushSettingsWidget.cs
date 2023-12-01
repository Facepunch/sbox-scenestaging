namespace Editor.TerrainEngine;


file class TextureWidget : Widget
{
	Pixmap pixmap;

	public TextureWidget( Pixmap pixmap, Widget parent ) : base( parent )
	{
		this.pixmap = pixmap;
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		Paint.Antialiasing = true;

		Paint.ClearPen();
		// Paint.SetBrush( Theme.Red );
		Paint.DrawRect( LocalRect );

		Paint.Draw( LocalRect.Contain( pixmap.Size ), pixmap );
	}
}

struct BrushData
{
	public string Name;
	public Texture Texture;
	public Pixmap Pixmap;
}

class BrushSettingsWidget : Widget
{
	public BrushSettingsWidget() : base( null )
	{
		List<BrushData> brushes = new();
		foreach ( var filename in FileSystem.Content.FindFile( "Brushes", "*.png" ) )
		{
			BrushData data = new BrushData();
			data.Name = System.IO.Path.GetFileNameWithoutExtension( filename );
			data.Texture = Texture.Load( FileSystem.Content, $"Brushes/{filename}" );
			data.Pixmap = Pixmap.FromFile( FileSystem.Content.GetFullPath( $"Brushes/{filename}" ) );
			brushes.Add( data );
		}

		ListView list = new();
		list.MaximumHeight = 80;
		list.SetItems( brushes.Cast<object>() );
		list.ItemSize = new Vector2( 32, 32 );
		list.OnPaintOverride += () => PaintListBackground( list );
		list.ItemPaint = PaintBrushItem;
		list.ItemSelected = ( item ) => { if ( item is BrushData data ) TerrainEditor.Brush.Set( data.Name ); };

		Layout = Layout.Column();

		Layout.Add( new Label( "Brushes" ) );

		var two = Layout.Row();

		two.Add( new Label( "current" ) );
		two.Add( list );

		Layout.Add( two );

		var brush = EditorUtility.GetSerializedObject( TerrainEditor.Brush );

		Log.Info( $"brush type: {brush.TypeName}" );

		foreach( var prop in brush )
		{
			Log.Info( $"\t{prop.Name} - {prop.PropertyType}" );
		}

		var radius = brush.GetProperty( nameof( Brush.Radius ) );

		Log.Info( $"radius: { radius.Name } { radius.PropertyType }" );

		var cs = new ControlSheet();

		// var radius = brush.GetProperty( nameof( Brush.Radius ) );
		Log.Info( $"source line: {radius.SourceLine} iseditable={radius.IsEditable}" );

		cs.AddRow( brush.GetProperty( nameof( Brush.Radius ) ) );
		cs.AddRow( brush.GetProperty( nameof( Brush.Strength ) ) );

		Layout.Add( cs );


		// Layout.Add
	}

	private void PaintBrushItem( VirtualWidget widget )
	{
		var brush = (BrushData)widget.Object;

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;

		if ( widget.Hovered || widget.Selected )
		{
			Paint.ClearPen();
			Paint.SetBrush( widget.Selected ? Theme.Primary : Color.White.WithAlpha( 0.1f ) );
			Paint.DrawRect( widget.Rect.Grow( 2 ), 3 );
		}

		Paint.Draw( widget.Rect, brush.Pixmap );
	}

	private bool PaintListBackground( Widget widget )
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( widget.LocalRect );

		return false;
	}
}
