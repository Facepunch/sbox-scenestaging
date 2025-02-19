namespace Editor.MovieMaker;

public class GridItem : GraphicsItem
{
	public GridItem()
	{
		ZIndex = 500;
	}

	protected override void OnPaint()
	{
		if ( Session.Current is not { } session ) return;

		foreach ( var (style, interval) in session.Ticks )
		{
			if ( style == TickStyle.TimeLabel ) continue;

			var dx = session.TimeToPixels( interval );
			var offset = SceneRect.Left % dx;

			Paint.SetPen( Theme.ControlText.WithAlpha( 0.02f ), style == TickStyle.Minor ? 0 : 2 );

			for ( float x = 0; x < Size.x + dx; x += dx )
			{
				if ( session.PixelsToTime( x ).IsNegative )
					continue;

				Paint.DrawLine( new Vector2( x - offset, 0 ), new Vector2( x - offset, Size.y ) );
			}
		}
	}
}
