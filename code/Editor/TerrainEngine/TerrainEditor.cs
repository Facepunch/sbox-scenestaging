using Editor.EntityPrefabEditor;
using Editor.Inspectors;
using Sandbox.TerrainEngine;
namespace Editor;

[CustomEditor( typeof( Terrain ) )]
public class TerrainControlWidget : ControlWidget
{
	public TerrainControlWidget( SerializedProperty property ) : base( property )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		Layout.Spacing = 2;

		AcceptDrops = true;
	}

	protected override Vector2 SizeHint() => new Vector2( 10000, 22 );

	protected override void OnContextMenu( ContextMenuEvent e )
	{
		var m = new Menu( this );

		m.OpenAtCursor( true );
	}

	protected override void PaintControl()
	{
		var rect = LocalRect.Shrink( 6, 0 );
		var component = SerializedProperty.GetValue<BaseComponent>();
		var type = EditorTypeLibrary.GetType( SerializedProperty.PropertyType );

		Paint.SetBrush( Theme.Red );
		Paint.DrawRect( rect );

/*		if ( component is null )
		{
			Paint.SetPen( Theme.ControlText.WithAlpha( 0.3f ) );
			Paint.DrawIcon( rect, type?.Icon, 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, $"None ({type?.Name})", TextFlag.LeftCenter );
			Cursor = CursorShape.None;
		}
		else
		{
			Paint.SetPen( Theme.Green );
			Paint.DrawIcon( rect, type?.Icon, 14, TextFlag.LeftCenter );
			rect.Left += 22;
			Paint.DrawText( rect, $"{component.GetType()} (on {component.GameObject.Name})", TextFlag.LeftCenter );
			Cursor = CursorShape.Finger;
		}*/
	}
}

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

[CustomEditor( typeof( Terrain ) )]
public class TerrainWidget : CustomComponentWidget
{
	public TerrainWidget( SerializedObject obj ) : base( obj )
	{
		SetSizeMode( SizeMode.Default, SizeMode.Default );

		Layout = Layout.Column();
		Layout.Spacing = 8;

		var tabs = Layout.Add( new TabWidget( this ) );
		tabs.AddPage( "Sculpt", "construction", SculptPage() );
		tabs.AddPage( "Paint", "brush", new Widget( this ) );
		tabs.AddPage( "Settings", "settings", SettingsPage() );
	}

	Widget SculptPage()
	{
		var container = new Widget( null );

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
		list.ItemSize = new Vector2( 48, 64 );
		list.OnPaintOverride += () => PaintListBackground( list );
		list.ItemPaint = PaintBrushItem;
		list.ItemSelected = ( item ) => { if ( item is BrushData data ) SerializedObject.GetProperty( "Brush" ).SetValue( data.Name ); };

		Layout.Add( list );

		container.Layout = Layout.Column();
		container.Layout.Spacing = 8;
		// container.Layout.Add( new InformationBox( "Left click to raise.\n\nHold shift and left click to lower." ) );
		container.Layout.Add( list );

		var cs = new ControlSheet();

		var radius = SerializedObject.GetProperty( "BrushRadius" );
		if ( radius != null ) cs.AddRow( radius );

		var strength = SerializedObject.GetProperty( "BrushStrength" );
		if ( strength != null ) cs.AddRow( strength );

		container.Layout.Add( cs );

		return container;
	}

	Widget SettingsPage()
	{
		var container = new Widget( null );

		var sheet = new ControlSheet();
		sheet.AddObject( SerializedObject );

		container.Layout = Layout.Column();
		container.Layout.Add( sheet );

		var warning = new WarningBox( "Heightmaps must use a single channel and be either 8 or 16 bit. Resolution must be power of two." );
		container.Layout.Add( warning );
		container.Layout.Add( new Button( "Import Heightmap" ) );

		return container;
	}

	private bool PaintListBackground( Widget widget )
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( widget.LocalRect );

		return false;
	}

	private void PaintBrushItem( VirtualWidget widget )
	{
		var brush = (BrushData)widget.Object;

		if ( widget.Hovered || widget.Selected )
		{
			Paint.ClearPen();
			Paint.SetBrush( widget.Selected ? Theme.Selection.WithAlpha( 0.8f ) : Color.White.WithAlpha( 0.1f ) );
			Paint.DrawRect( widget.Rect.Grow( 2 ), 3 );
		}

		Paint.Draw( widget.Rect.Shrink( 0, 0, 0, 16 ), brush.Pixmap );

		Paint.SetPen( Theme.White.WithAlpha( 0.7f ) );
		Paint.DrawText( widget.Rect, brush.Name, TextFlag.CenterBottom );
	}
}
