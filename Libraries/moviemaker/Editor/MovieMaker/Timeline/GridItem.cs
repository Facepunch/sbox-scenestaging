using Sandbox.MovieMaker;
using Sandbox.Services;

namespace Editor.MovieMaker;

public class BackgroundItem : GraphicsItem
{
	public Session Session { get; }

	public BackgroundItem( Session session )
	{
		ZIndex = -10_000;
		Session = session;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( LocalRect );

		if ( Session.SequenceTimeRange is { } sequenceRange )
		{
			Paint.SetBrushAndPen( Theme.ControlBackground.LerpTo( Timeline.Colors.Background, 0.5f ) );
			DrawTimeRangeRect( (MovieTime.Zero, Session.Project.Duration) );

			Paint.SetBrushAndPen( Timeline.Colors.Background );
			DrawTimeRangeRect( sequenceRange );
		}
		else
		{
			Paint.SetBrushAndPen( Timeline.Colors.Background );
			DrawTimeRangeRect( (MovieTime.Zero, Session.Project.Duration) );
		}
	}

	private void DrawTimeRangeRect( MovieTimeRange timeRange )
	{
		var startX = FromScene( Session.TimeToPixels( timeRange.Start ) ).x;
		var endX = FromScene( Session.TimeToPixels( timeRange.End ) ).x;

		Paint.DrawRect( new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) ) );
	}

	private int _lastState;

	public void Frame()
	{
		var state = HashCode.Combine( Session.PixelsPerSecond, Session.TimeOffset, Session.Project.Duration );

		if ( state != _lastState )
		{
			_lastState = state;
			Update();
		}
	}
}

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

			Paint.SetPen( Theme.TextControl.WithAlpha( 0.02f ), style == TickStyle.Minor ? 0 : 2 );

			for ( var x = 0f; x < Size.x + dx; x += dx )
			{
				if ( Session.PixelsToTime( x + Position.x ) <= -interval )
					continue;

				Paint.DrawLine( new Vector2( x - offset, 0 ), new Vector2( x - offset, Size.y ) );
			}
		}
	}
}
