using Editor;

namespace Editor.Inspectors;

[Inspector( typeof( IProceduralScatterer ) )]
public class ScattererInspector : InspectorWidget
{
	const float HeaderHeight = 64 + 8 + 8;

	public IProceduralScatterer Scatterer { get; set; }

	public ScattererInspector( SerializedObject so ) : base( so )
	{
		Scatterer = so.Targets.Cast<IProceduralScatterer>().First();

		Layout = Layout.Column();
		Layout.Margin = new( 0, HeaderHeight + 8, 0, 0 );
		SetSizeMode( SizeMode.CanGrow, SizeMode.Default );

		var scroller = new ScrollArea( this );
		scroller.Canvas = new Widget( this );
		scroller.Canvas.Layout = Layout.Column();
		Layout.Add( scroller, 1 );

		var sheet = new ControlSheet();
		sheet.IncludePropertyNames = true;
		scroller.Canvas.Layout.Add( sheet );
		sheet.AddObject( so );
		scroller.Canvas.Layout.AddStretchCell();
	}

	protected override void OnPaint()
	{
		// Background bar
		Paint.SetBrushAndPen( Theme.ControlBackground.WithAlpha( 0.4f ) );
		Paint.ClearPen();
		Paint.DrawRect( new Rect( new Vector2( 0, HeaderHeight - 26 ), new Vector2( Width, 26 ) ) );

		Paint.RenderMode = RenderMode.Screen;
		var pos = new Vector2( 64 + 16 + 4, 8 );
		Paint.SetPen( Theme.Text );
		Paint.SetHeadingFont( 13, 450 );

		var title = Scatterer.ToString();
		var subtitle = Scatterer.GetType().Name;

		var r = Paint.DrawText( pos, title );
		pos.y = r.Bottom;

		Paint.SetPen( Theme.Primary.WithAlpha( 0.9f ) );
		Paint.SetDefaultFont();
		Paint.DrawText( pos, subtitle );

		// Draw icon
		Paint.RenderMode = RenderMode.Screen;
		Paint.SetPen( Color.White.WithAlpha( 0.8f ) );
		var iconRect = new Rect( 8, 8, 64, 64 );
		Paint.DrawIcon( iconRect, "forest", 64, TextFlag.Center );
	}
}
