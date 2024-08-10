
namespace Editor.MovieMaker;

/// <summary>
/// A bar with times and notches on it
/// </summary>
public class ScrubberWidget : Widget
{
	public MovieEditor Editor { get; private set; }
	public Session Session { get; private set; }

	public ScrubberWidget( MovieEditor timelineEditor ) : base( timelineEditor )
	{
		Session = timelineEditor.Session;
		this.Editor = timelineEditor;
		MinimumHeight = 24;

		TranslucentBackground = false;
		NoSystemBackground = false;
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		Session.SetCurrentPointer( GetTimeAt( e.LocalPosition.x ) );
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		Session.SetCurrentPointer( GetTimeAt( e.LocalPosition.x ) );
	}

	public float GetTimeAt( float x )
	{
		var zero = Editor.TrackList.RightWidget.ToScreen( 0 );
		zero = FromScreen( zero );
		zero.x += 8;

		var time = Session.PixelsToTime( x - zero.x );
		time += Session.TimeOffset;

		return time;
	}

	public float ToPixels( float time )
	{
		var zero = Editor.TrackList.RightWidget.ToScreen( 0 );
		zero = FromScreen( zero );
		zero.x += 8;

		var pixels = Session.TimeToPixels( time );
		pixels -= Session.TimeToPixels( Session.TimeOffset );

		return pixels + zero.x;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( TrackDopesheet.Colors.Background );
		Paint.DrawRect( LocalRect );

		Paint.Pen = Color.White.WithAlpha( 0.1f );
		Paint.PenSize = 3;
		Paint.DrawLine( LocalRect.BottomLeft, LocalRect.BottomRight );

		Paint.Antialiasing = true;

		float zero = GetTimeAt( 0 );
		var oneSecond = Session.TimeToPixels( 1 );

		var mul = 1;

		while ( (oneSecond * mul) < 70 )
		{
			mul *= 5;
		}

		oneSecond *= mul;
		var timeOffset = Session.TimeToPixels( zero ) % oneSecond;

		Paint.SetFont( "Roboto", 8, 300 );

		for ( float x = -timeOffset; x < Width; x += oneSecond )
		{
			float time = GetTimeAt( x );
			if ( time <= -0.1f ) continue;

			{
				Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
				Paint.DrawLine( new Vector2( x, 4 ), new Vector2( x, Height - 2 ) );
			}


			{
				var ts = TimeSpan.FromSeconds( (int)(time + 0.5f) );
				Paint.SetPen( Theme.Green.WithAlpha( 0.2f ) );
				Paint.DrawText( new Vector2( x + 6, 2 ), $"{ts}" );


				if ( oneSecond > 80 )
				{
					Paint.SetPen( Color.White.WithAlpha( oneSecond.Remap( 30, 100, 0, 0.1f ) ) );
					float tength = oneSecond / 10.0f;
					for ( int i = 1; i < 10; i++ )
					{
						Paint.DrawLine( new Vector2( x + tength * i, Height - 8 ), new Vector2( x + tength * i, Height - 2 ) );
					}
				}
			}
		}

		// Cursor
		{
			var pos = ToPixels( Session.CurrentPointer );
			Paint.SetBrushAndPen( Theme.Yellow );
			Extensions.PaintBookmarkDown( pos, Height, 4, 4, 12 );
		}

	}

	int lastState;


	[EditorEvent.Frame]
	public void Frame()
	{
		var state = HashCode.Combine( Session.TimeVisible, Session.TimeOffset, Session.CurrentPointer );

		if ( state != lastState )
		{
			lastState = state;
			Update();
		}
	}
}

