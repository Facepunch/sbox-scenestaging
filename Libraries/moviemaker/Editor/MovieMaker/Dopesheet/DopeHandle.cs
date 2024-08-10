
using System.Linq;

namespace Editor.MovieMaker;

public class DopeHandle : GraphicsItem
{
	public float Time { get; set; }
	public object Value { get; set; }

	private DopesheetTrack track;

	public DopesheetTrack Track => track;

	Color HandleColor;

	public DopeHandle( DopesheetTrack parent ) : base( parent )
	{
		this.track = parent;

		HoverEvents = true;
		HandlePosition = new Vector2( 0.5f, 0.0f );
		Size = 16.0f;
		Cursor = CursorShape.Finger;
		Movable = true;
		Focusable = true;
		Selectable = true;

		HandleColor = track.HandleColor;
	}

	protected override void OnMoved()
	{
		base.OnMoved();

		Time = Session.Current.PixelsToTime( Position.x, true );

		if ( Time < 0 ) Time = 0;

		track.Write();

		var pixels = Session.Current.TimeToPixels( Time );

		UpdatePosition();

		Session.Current.SetCurrentPointer( Time );
	}

	protected override void OnSelectionChanged()
	{
		base.OnSelectionChanged();

		if ( Selected && GraphicsView.SelectedItems.Count() <= 1 )
		{
			track.OnSelected();
		}
	}



	protected override void OnPaint()
	{
		var c = Extensions.PaintSelectColor( HandleColor.WithAlpha( 0.5f ), HandleColor, TrackDopesheet.Colors.HandleSelected );
		var b = Extensions.PaintSelectColor( Color.Black, Theme.Blue, Theme.White );

		var w = Width;
		var h = Height;

		Paint.SetBrushAndPen( c );

		Extensions.PaintTriangle( Size * 0.5f, 10 );

		Paint.SetPen( c.WithAlphaMultiplied( 0.3f ) );
		Paint.DrawLine( new Vector2( Width * 0.5f, 0 ), new Vector2( Width * 0.5f, Height ) );
	}

	internal void UpdatePosition()
	{
		Position = new Vector2( Session.Current.TimeToPixels( Time ), 0 );
		Size = new Vector2( 16, Parent.Height );
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		if ( e.LeftMouseButton )
		{
			Session.Current.SetCurrentPointer( Time );
		}
	}

	internal void Nudge( float v )
	{
		Time += v;
		if ( Time < 0 ) Time = 0;
		UpdatePosition();
	}
}
