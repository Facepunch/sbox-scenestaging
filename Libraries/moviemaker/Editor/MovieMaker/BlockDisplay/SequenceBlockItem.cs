using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public sealed class SequenceBlockItem : BlockItem<MovieResource?>
{
	private RealTimeSince _lastClick;

	public SequenceBlockItem()
	{
		HoverEvents = true;
		Selectable = true;

		Cursor = CursorShape.Finger;
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		e.Accepted = true;
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		if ( _lastClick < 0.5f )
		{
			DoubleClicked();
		}

		_lastClick = 0f;

		e.Accepted = true;
	}

	private void DoubleClicked()
	{
		if ( Block.GetValue( TimeRange.Start ) is { } resource )
		{
			Parent.Session.Editor.SwitchResource( resource );
		}
	}

	protected override void OnPaint()
	{
		var isHovered = Paint.HasMouseOver;

		Paint.SetBrushAndPen( Theme.Primary.Darken( isHovered ? 0.1f : 0.25f ) );
		Paint.DrawRect( LocalRect, 2 );

		var viewMin = FromScene( Parent.GraphicsView.SceneRect.TopLeft );
		var viewMax = FromScene( Parent.GraphicsView.SceneRect.BottomRight );
		var minX = Math.Max( LocalRect.Left, viewMin.x );
		var maxX = Math.Min( LocalRect.Right, viewMax.x );

		Paint.SetBrush( Theme.ControlBackground );

		for ( var x = minX.SnapToGrid( 8f ); x <= maxX; x += 8f )
		{
			Paint.DrawRect( new Rect( x - 2f, LocalRect.Top + 2f, 4f, 6f ) );
		}

		Paint.ClearBrush();
		Paint.SetPen( Theme.ControlText );

		Paint.DrawText( LocalRect.Shrink( 8f, 4f, 8f, 0f ), Block.GetValue( TimeRange.Start )?.ResourcePath );
	}
}
