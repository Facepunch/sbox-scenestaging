namespace Editor.MovieMaker;

/// <summary>
/// A bar with times and notches on it
/// </summary>
public class ScrubberWidget : Widget
{
	public MovieEditor Editor { get; }
	public Session Session { get; }

	public bool IsTop { get; }

	public ScrubberWidget( MovieEditor timelineEditor, bool isTop ) : base( timelineEditor )
	{
		Session = timelineEditor.Session;
		Editor = timelineEditor;
		MinimumHeight = 24;
		IsTop = isTop;

		TranslucentBackground = false;
		NoSystemBackground = false;
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
		var duration = Session.Clip?.Duration ?? 0f;

		Paint.SetBrushAndPen( DopeSheet.Colors.Background );
		Paint.DrawRect( LocalRect );

		// Darker background for the clip duration

		var startX = ToPixels( 0f );
		var endX = ToPixels( duration );

		Paint.SetBrushAndPen( DopeSheet.Colors.ChannelBackground );
		Paint.DrawRect( new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) ) );

		Paint.Pen = Color.White.WithAlpha( 0.1f );
		Paint.PenSize = 2;

		if ( IsTop )
		{
			Paint.DrawLine( LocalRect.BottomLeft, LocalRect.BottomRight );
		}
		else
		{
			Paint.DrawLine( LocalRect.TopLeft, LocalRect.TopRight );
		}

		Paint.Antialiasing = true;

		float zero = GetTimeAt( 0 );
		var oneSecond = Session.TimeToPixels( 1 );

		var mul = 1;

		while ( oneSecond * mul < 70 )
		{
			mul *= 5;
		}

		oneSecond *= mul;
		var timeOffset = Session.TimeToPixels( zero ) % oneSecond;

		Paint.SetFont( "Roboto", 8, 300 );

		var tickMargin = 2f;

		var smallTickHeight = 6f;
		var bigTickHeight = Height - 6f;

		var smallTickY = IsTop ? Height - smallTickHeight - tickMargin : tickMargin;
		var bigTickY = IsTop ? Height - bigTickHeight - tickMargin : tickMargin;

		for ( var x = -timeOffset; x < Width; x += oneSecond )
		{
			var time = GetTimeAt( x );
			if ( time <= -0.1f ) continue;

			{
				Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
				Paint.DrawLine( new Vector2( x, bigTickY ), new Vector2( x, bigTickY + bigTickHeight ) );
			}

			{
				var ts = TimeSpan.FromSeconds( (int)(time + 0.5f) );
				Paint.SetPen( Theme.Green.WithAlpha( 0.2f ) );
				Paint.DrawText( new Vector2( x + 6, IsTop ? 2 : Height - 14f ), $"{ts}" );

				if ( oneSecond > 80 )
				{
					Paint.SetPen( Color.White.WithAlpha( oneSecond.Remap( 30, 100, 0, 0.1f ) ) );
					var tenth = oneSecond / 10.0f;
					for ( var i = 1; i < 10; i++ )
					{
						Paint.DrawLine( new Vector2( x + tenth * i, smallTickY ), new Vector2( x + tenth * i, smallTickY + smallTickHeight ) );
					}
				}
			}
		}

		Editor.Session.EditMode?.ScrubberPaint( this );

		DrawPointer( Session.CurrentPointer, Theme.Yellow );

		if ( Session.PreviewPointer is { } preview )
		{
			DrawPointer( preview, Theme.Blue.WithAlpha( 0.5f ) );
		}
	}

	public void DrawPointer( float time, Color color )
	{
		var pos = ToPixels( time );
		Paint.SetBrushAndPen( color );

		if ( IsTop )
		{
			Extensions.PaintBookmarkDown( pos, Height, 4, 4, 12 );
		}
		else
		{
			Extensions.PaintBookmarkUp( pos, 0f, 4, 4, 12 );
		}
	}

	int lastState;


	[EditorEvent.Frame]
	public void Frame()
	{
		var state = HashCode.Combine( Session.PixelsPerSecond, Session.TimeOffset, Session.CurrentPointer, Session.PreviewPointer, Session.Clip?.Duration );

		if ( state != lastState )
		{
			lastState = state;
			Update();
		}
	}
}

