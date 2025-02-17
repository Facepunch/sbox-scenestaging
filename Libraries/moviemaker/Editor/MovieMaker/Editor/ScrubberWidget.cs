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

		Session.SetCurrentPointer( GetTimeAt( e.LocalPosition.x, true ) );
	}

	protected override void OnMouseMove( MouseEvent e )
	{
		base.OnMouseMove( e );

		Session.SetCurrentPointer( GetTimeAt( e.LocalPosition.x, true ) );
	}

	public float GetTimeAt( float x, bool snap = false )
	{
		var zero = Editor.TrackList.RightWidget.ToScreen( 0 );
		zero = FromScreen( zero );
		zero.x += 8;

		var time = Session.PixelsToTime( x - zero.x, snap );
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
		Paint.SetFont( "Roboto", 8, 300 );

		var zero = GetTimeAt( 0 );

		foreach ( var (style, interval) in Session.Ticks )
		{
			var dx = Session.TimeToPixels( interval );
			var timeOffset = Session.TimeToPixels( zero ) % dx;
			var height = Height;
			var margin = 2f;

			switch ( style )
			{
				case TickStyle.TimeLabel:
					Paint.SetPen( Theme.Green.WithAlpha( 0.2f ) );
					height -= 12f;
					margin = 10f;
					break;

				case TickStyle.Major:
					Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
					height -= 6f;
					break;

				case TickStyle.Minor:
					Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
					height = 6f;
					break;
			}

			var y = IsTop ? Height - height - margin : margin;

			for ( var x = -timeOffset; x < Width; x += dx )
			{
				var time = GetTimeAt( x );
				if ( time <= -0.0005f ) continue;

				if ( style == TickStyle.TimeLabel )
				{
					Paint.SetPen( Theme.Green.WithAlpha( 0.2f ) );
					Paint.DrawText( new Vector2( x + 6, y ), TimeToString( time, interval ) );
				}
				else
				{
					Paint.DrawLine( new Vector2( x, y ), new Vector2( x, y + height ) );
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

	private static string TimeToString( float time, float interval )
	{
		return TimeSpan.FromSeconds( time + 0.00049f ).ToString( @"mm\:ss\.fff" );
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

