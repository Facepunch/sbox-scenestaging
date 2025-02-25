namespace Editor.MovieMaker;

public class CurrentPointerItem : GraphicsItem
{
	public Color Color { get; }

	public CurrentPointerItem( Color color )
	{
		Color = color;

		ZIndex = 10000;
		HandlePosition = new Vector2( 0.5f, 0 );
	}

	protected override void OnPaint()
	{
		Paint.SetPen( Color.WithAlpha( 0.5f ) );
		Paint.DrawLine( new Vector2( 0f, 12f ), new Vector2( 0, Height - 12f ) );
		Paint.SetBrushAndPen( Color );

		PaintExtensions.PaintBookmarkDown( Width * 0.5f, 12f, 4, 4, 12 );
		PaintExtensions.PaintBookmarkUp( Width * 0.5f, Height - 12f, 4, 4, 12 );
	}
}
