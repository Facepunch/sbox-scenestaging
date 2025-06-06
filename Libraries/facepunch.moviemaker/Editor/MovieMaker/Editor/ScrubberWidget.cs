﻿using Sandbox.MovieMaker;

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

		ToolTip = "Click and drag to define a time range to loop when previewing.";
	}

	private MovieTime _dragStartTime;

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		_dragStartTime = Session.ScenePositionToTime( ToScene( e.LocalPosition ) );

		Session.LoopTimeRange = null;

		e.Accepted = true;

		Update();
	}

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		base.OnMouseMove( e );

		var time = Session.ScenePositionToTime( ToScene( e.LocalPosition ) );

		if ( time != _dragStartTime )
		{
			Session.LoopTimeRange = new MovieTimeRange(
				MovieTime.Min( time, _dragStartTime ),
				MovieTime.Max( time, _dragStartTime ) );
		}
		else
		{
			Session.LoopTimeRange = null;
		}

		Update();
	}

	protected override void OnPaint()
	{
		var duration = Session.Project.Duration;

		Paint.SetBrushAndPen( Timeline.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect );

		// Darker background for the clip duration

		{
			var timeRange = Session.SequenceTimeRange ?? (MovieTime.Zero, duration);

			var startX = FromScene( Session.TimeToPixels( timeRange.Start ) ).x;
			var endX = FromScene( Session.TimeToPixels( timeRange.End ) ).x;

			Paint.SetBrushAndPen( Timeline.Colors.Background );
			Paint.DrawRect( new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) ) );
		}

		// Paste time range

		if ( Session.EditMode?.SourceTimeRange is { } pasteRange )
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

		// Loop time range

		if ( Session.LoopTimeRange is { } loopRange )
		{
			var startX = FromScene( Session.TimeToPixels( loopRange.Start ) ).x;
			var endX = FromScene( Session.TimeToPixels( loopRange.End ) ).x;

			var rect = new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) );

			Paint.SetBrushAndPen( Color.White.WithAlpha( 0.05f ) );
			Paint.DrawRect( rect );

			Paint.ClearBrush();
			Paint.SetPen( Color.White );
			Paint.DrawLine( rect.TopLeft, rect.BottomLeft );
			Paint.DrawLine( rect.TopRight, rect.BottomRight );

			var top = IsTop ? rect.Bottom : rect.Top;
			var up = IsTop ? new Vector2( 0f, -1f ) : new Vector2( 0f, 1f );
			var leftCorner = new Vector2( rect.Left, top );
			var rightCorner = new Vector2( rect.Right, top );

			Paint.SetBrush( Color.White.WithAlpha( 0.5f ) );
			Paint.DrawPolygon( leftCorner, leftCorner + up * 6f, leftCorner + new Vector2( 6f, 0f ) );
			Paint.DrawPolygon( rightCorner, rightCorner + up * 6f, rightCorner - new Vector2( 6f, 0f ) );
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

			var t0 = MovieTime.Max( (range.Start - interval).Round( interval ), MovieTime.Zero );
			var t1 = t0 + range.Duration + interval;

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
}
