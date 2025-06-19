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
		Selectable = true;

		ToolTip =
			"""
			<h3>Scrubber</h3>
			<p><b>Click</b> to set playhead time, <b>Drag</b> to scrub.</p>
			<p><b>Alt+Click+Drag</b> to set a loop time range for previewing playback, <b>Alt+Click</b> to clear loop.</p>
			""";
	}

	private MovieTime _dragStartTime;
	private float _panSpeed;

	public void Frame()
	{
		Cursor = (Application.KeyboardModifiers & KeyboardModifiers.Alt) != 0
			? CursorShape.IBeam
			: CursorShape.Finger;

		if ( !_panSpeed.AlmostEqual( 0f ) )
		{
			var delta = -_panSpeed * RealTime.Delta;

			Session.ScrollBy( delta, false );

			_lastMouseEvent.ScenePos -= delta;

			OnMouseMove();
		}
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		var time = Session.ScenePositionToTime( ToScene( e.LocalPosition ), new SnapOptions( SnapFlag.Playhead ) );

		if ( e.MiddleMouseButton )
		{
			// Panning handled by timeline

			return;
		}

		if ( e.RightMouseButton )
		{
			ShowContextMenu( time );

			e.Accepted = true;
			return;
		}

		if ( e.HasAlt )
		{
			// Alt+Click: set loop time range

			_dragStartTime = time;
			Session.LoopTimeRange = null;
		}
		else
		{
			// Click: set playhead time

			Session.PlayheadTime = time;
		}

		e.Accepted = true;

		_panSpeed = 0f;

		Update();
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		base.OnMouseReleased(e);

		_panSpeed = 0f;
	}

	private void ShowContextMenu( MovieTime time )
	{
		var menu = new Menu();

		menu.AddHeading( "Preview Loop" );

		menu.AddOption( "Set Start", "start", () =>
		{
			Session.LoopTimeRange = Session.LoopTimeRange is not { } range || range.End <= time
				? (time, Session.Project.Duration)
				: (time, range.End);
		} ).Enabled = Session.LoopTimeRange is null || Session.LoopTimeRange.Value.End > time;

		menu.AddOption( "Set End", "last_page", () =>
		{
			Session.LoopTimeRange = Session.LoopTimeRange is not { } range || range.Start >= time
				? (0d, time)
				: (range.Start, time);
		} ).Enabled = Session.LoopTimeRange is null || Session.LoopTimeRange.Value.Start < time;

		menu.AddOption( "Clear", "clear", () =>
		{
			Session.LoopTimeRange = null;
		} ).Enabled = Session.LoopTimeRange is not null;

		menu.OpenAtCursor();
	}

	private (MouseButtons Buttons, KeyboardModifiers KeyboardModifiers, Vector2 ScenePos) _lastMouseEvent;

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		_lastMouseEvent = (e.Buttons, e.KeyboardModifiers, ToScene( e.LocalPosition ));

		OnMouseMove();
	}

	private void OnMouseMove()
	{
		var (buttons, modifiers, scenePos) = _lastMouseEvent;

		if ( (buttons & MouseButtons.Left) == 0 )
		{
			return;
		}

		var sceneView = GraphicsView.SceneRect;

		if ( scenePos.x > sceneView.Right )
		{
			_panSpeed = (scenePos.x - sceneView.Right) * 5f;
			scenePos.x = sceneView.Right;
		}
		else if ( scenePos.x < sceneView.Left )
		{
			_panSpeed = (scenePos.x - sceneView.Left) * 5f;
			scenePos.x = sceneView.Left;
		}
		else
		{
			_panSpeed = 0f;
		}

		var time = Session.ScenePositionToTime( scenePos, new SnapOptions( SnapFlag.Playhead ) );

		if ( (modifiers & KeyboardModifiers.Alt) != 0 )
		{
			if ( time != _dragStartTime )
			{
				// Alt+Click+Drag: set loop time range

				Session.LoopTimeRange = new MovieTimeRange(
					MovieTime.Min( time, _dragStartTime ),
					MovieTime.Max( time, _dragStartTime ) );
			}
			else
			{
				// Alt+Click: clear loop time range

				Session.LoopTimeRange = null;
			}
		}
		else
		{
			// Click: set playhead time

			Session.PlayheadTime = time;
		}

		Update();
	}

	protected override void OnPaint()
	{
		var duration = Session.Project.Duration;

		Paint.SetBrushAndPen( Timeline.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect );

		// Darker background for the clip duration

		if ( Session.SequenceTimeRange is { } sequenceRange )
		{
			Paint.SetBrushAndPen( Theme.ControlBackground.LerpTo( Timeline.Colors.Background, 0.5f ) );
			DrawTimeRangeRect( (MovieTime.Zero, duration) );

			Paint.SetBrushAndPen( Timeline.Colors.Background );
			DrawTimeRangeRect( sequenceRange );
		}
		else
		{
			Paint.SetBrushAndPen( Timeline.Colors.Background );
			DrawTimeRangeRect( (MovieTime.Zero, duration) );
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

	private void DrawTimeRangeRect( MovieTimeRange timeRange )
	{
		var startX = FromScene( Session.TimeToPixels( timeRange.Start ) ).x;
		var endX = FromScene( Session.TimeToPixels( timeRange.End ) ).x;

		Paint.DrawRect( new Rect( new Vector2( startX, LocalRect.Top ), new Vector2( endX - startX, LocalRect.Height ) ) );
	}

	private static string TimeToString( MovieTime time, MovieTime interval )
	{
		return time.ToString();
	}
}
