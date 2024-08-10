namespace Editor;

public static class Extensions
{
	/// <summary>
	/// A bookmark shape, pointing down. Reminds me of those things you move for snooker scores
	/// </summary>
	public static void PaintBookmarkDown( float x, float bottom, float width, float arrowheight, float totalheight )
	{
		Paint.DrawPolygon( new Vector2( x, bottom ), new Vector2( x + width, bottom - arrowheight ), new Vector2( x + width, bottom - totalheight ), new Vector2( x - width, bottom - totalheight ), new Vector2( x - width, bottom - arrowheight ) );
	}

	/// <summary>
	/// A triangle shape
	/// </summary>
	public static void PaintTriangle( Vector2 center, Vector2 size )
	{
		var x = new Vector2( size.x * 0.5f, 0 );
		var y = new Vector2( 0, size.y * 0.5f );

		Paint.DrawPolygon( center - x, center - y, center + x, center + y );
	}

	public static Color PaintSelectColor( Color normal, Color hover, Color selected )
	{
		if ( Paint.HasSelected || Paint.HasFocus ) return selected;
		if ( Paint.HasMouseOver ) return hover;
		return normal;
	}
}
