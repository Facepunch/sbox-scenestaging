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
		Paint.DrawLine( 0, new Vector2( 0, Size.y ) );
	}
}
