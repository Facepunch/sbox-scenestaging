namespace Editor.MovieMaker;

public class GridItem : GraphicsItem
{
	public Session Session { get; }

	public GridItem( Session session )
	{
		ZIndex = 500;
		Session = session;
	}

	protected override void OnPaint()
	{
		foreach ( var (style, interval) in Session.Ticks )
		{
			if ( style == TickStyle.TimeLabel ) continue;

			var dx = Session.TimeToPixels( interval );
			var offset = SceneRect.Left % dx;

			Paint.SetPen( Theme.ControlText.WithAlpha( 0.02f ), style == TickStyle.Minor ? 0 : 2 );

			for ( float x = 0; x < Size.x + dx; x += dx )
			{
				if ( Session.PixelsToTime( x ).IsNegative )
					continue;

				Paint.DrawLine( new Vector2( x - offset, 0 ), new Vector2( x - offset, Size.y ) );
			}
		}
	}
}
