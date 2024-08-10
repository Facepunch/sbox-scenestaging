namespace Editor.MovieMaker;

public class CurrentPointerItem : GraphicsItem
{
	public CurrentPointerItem()
	{
		ZIndex = 10000;
		HandlePosition = new Vector2( 0.5f, 0 );
	}

	protected override void OnPaint()
	{
		Paint.SetPen( Theme.Yellow.WithAlpha( 0.5f ) );
		Paint.DrawLine( 0, new Vector2( 0, Size.y ) );

	}
}
