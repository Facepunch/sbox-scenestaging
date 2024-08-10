namespace Editor.MovieMaker;

public class GridItem : GraphicsItem
{
	public GridItem()
	{
		ZIndex = 500;
	}

	protected override void OnPaint()
	{
		float oneSecond = Session.Current.TimeToPixels( 1 );


		var mul = 1;

		while ( (oneSecond * mul) < 70 )
		{
			mul *= 5;
		}

		oneSecond *= mul;

		float offset = SceneRect.Left % oneSecond;


		// 1 / 10
		Paint.SetPen( Theme.ControlText.WithAlpha( 0.02f ) );
		for ( float x = 0; x < Size.x + oneSecond; x += oneSecond / 10.0f )
		{
			if ( Session.Current.PixelsToTime( x ) < 0 )
				continue;


			Paint.DrawLine( new Vector2( x - offset, 0 ), new Vector2( x - offset, Size.y ) );
		}

		Paint.SetPen( Theme.ControlText.WithAlpha( 0.02f ), 2 );
		for ( float x = 0; x < Size.x + oneSecond; x += oneSecond )
		{
			if ( Session.Current.PixelsToTime( x ) < 0 )
				continue;

			Paint.DrawLine( new Vector2( x - offset, 0 ), new Vector2( x - offset, Size.y ) );
		}
	}

}
