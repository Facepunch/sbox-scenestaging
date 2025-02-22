using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

/// <summary>
/// A bar with times and notches on it
/// </summary>
public class ScrubberItem : GraphicsItem
{
	public MovieEditor Editor { get; }
	public Session Session { get; }

	public bool IsTop { get; }

	public ScrubberItem( MovieEditor timelineEditor, bool isTop )
	{
		Session = timelineEditor.Session;
		Editor = timelineEditor;
		IsTop = isTop;

		ZIndex = 5000;

		HoverEvents = true;
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		Session.SetCurrentPointer( Session.ScenePositionToTime( ToScene( e.LocalPosition ), ignore: SnapFlag.PlayHead ) );

		e.Accepted = true;
	}

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		base.OnMouseMove( e );

		Session.SetCurrentPointer( Session.ScenePositionToTime( ToScene( e.LocalPosition ), ignore: SnapFlag.PlayHead ) );
	}

	protected override void OnPaint()
	{
		var duration = Session.Clip?.Duration ?? MovieTime.Zero;

		Paint.SetBrushAndPen( DopeSheet.Colors.Background );
		Paint.DrawRect( LocalRect );

		// Darker background for the clip duration

		{
			var startX = FromScene( Session.TimeToPixels( MovieTime.Zero ) ).x;
			var endX = FromScene( Session.TimeToPixels( duration ) ).x;

			Paint.SetBrushAndPen( DopeSheet.Colors.ChannelBackground );
			Paint.DrawRect( new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) ) );
		}

		// Paste time range

		if ( Session.EditMode?.PasteTimeRange is { } pasteRange )
		{
			var startX = FromScene( Session.TimeToPixels( pasteRange.Start ) ).x;
			var endX = FromScene( Session.TimeToPixels( pasteRange.End ) ).x;

			var rect = new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) );

			Paint.SetBrushAndPen( Color.White.WithAlpha( 0.2f ) );
			Paint.DrawRect( rect );

			Paint.PenSize = 1;
			Paint.Pen = Color.White.WithAlpha( 0.5f );
			Paint.DrawLine( rect.TopLeft, rect.BottomLeft );
			Paint.DrawLine( rect.TopRight, rect.BottomRight );
			Paint.DrawIcon( rect, "content_paste", 16f );
		}

		var range = Session.VisibleTimeRange;

		Paint.PenSize = 2;
		Paint.Pen = Color.White.WithAlpha( 0.1f );

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

		foreach ( var (style, interval) in Session.Ticks )
		{
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

			var t0 = MovieTime.Max( (range.Start - interval).SnapToGrid( interval ), MovieTime.Zero );
			var t1 = t0 + range.Duration;

			for ( var t = t0; t <= t1; t += interval )
			{
				var x = FromScene( Session.TimeToPixels( t ) ).x;

				if ( style == TickStyle.TimeLabel )
				{
					var time = Session.PixelsToTime( ToScene( x ).x );

					Paint.SetPen( Theme.Green.WithAlpha( 0.2f ) );
					Paint.DrawText( new Vector2( x + 6, y ), TimeToString( time, interval ) );
				}
				else
				{
					Paint.DrawLine( new Vector2( x, y ), new Vector2( x, y + height ) );
				}
			}
		}
	}

	private static string TimeToString( MovieTime time, MovieTime interval )
	{
		return time.ToString();
	}

	int lastState;

	[EditorEvent.Frame]
	public void Frame()
	{
		var state = HashCode.Combine( Session.PixelsPerSecond, Session.TimeOffset, Session.CurrentPointer,
			Session.PreviewPointer, Session.Clip?.Duration, Session.EditMode?.PasteTimeRange );

		if ( state != lastState )
		{
			lastState = state;
			Update();
		}
	}
}

