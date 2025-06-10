
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public sealed class KeyframeHandle : GraphicsItem, IComparable<KeyframeHandle>
{
	private Keyframe _keyframe;

	public new TimelineTrack Parent { get; }
	public Session Session { get; }
	public TrackView View { get; }

	public KeyframeEditMode? EditMode => Session.EditMode as KeyframeEditMode;

	public Keyframe Keyframe
	{
		get => _keyframe;
		set
		{
			_keyframe = value;
			UpdatePosition();
		}
	}

	public MovieTime Time
	{
		get => Keyframe.Time;
		set => Keyframe = Keyframe with { Time = value };
	}

	public KeyframeHandle( TimelineTrack parent, Keyframe keyframe )
		: base( parent )
	{
		Parent = parent;
		Session = parent.Session;
		View = parent.View;

		_keyframe = keyframe;

		HandlePosition = new Vector2( 0.5f, 0f );
		ZIndex = 100;

		HoverEvents = true;

		Focusable = true;
		Selectable = true;

		Cursor = CursorShape.Finger;

		UpdatePosition();
	}

	public void UpdatePosition()
	{
		PrepareGeometryChange();

		Position = new Vector2( Session.TimeToPixels( Time ), 0f );
		Size = new Vector2( 16f, Parent.Height );

		Update();
	}

	protected override void OnSelectionChanged()
	{
		base.OnSelectionChanged();
		UpdatePosition();
	}

	protected override void OnPaint()
	{
		if ( View.IsLocked ) return;

		Paint.ClearPen();
		Paint.SetBrushRadial( LocalRect.Center, Width * 0.5f, Timeline.Colors.ChannelBackground, Color.Transparent );
		Paint.DrawRect( LocalRect );

		var c = PaintExtensions.PaintSelectColor( Parent.HandleColor.WithAlpha( 0.5f ), Parent.HandleColor, Timeline.Colors.HandleSelected );

		Paint.SetBrushAndPen( c );

		switch ( Keyframe.Interpolation )
		{
			case KeyframeInterpolation.Linear:
				PaintExtensions.PaintTriangle( Size * 0.5f, 10 );
				break;

			case KeyframeInterpolation.Quadratic:
				Paint.DrawCircle( Size * 0.5f, 8f );
				break;

			case KeyframeInterpolation.Cubic:
				Paint.DrawCircle( Size * 0.5f, 6f );
				Paint.ClearBrush();

				Paint.SetPen( c );
				Paint.DrawCircle( Size * 0.5f, 10f );
				break;
		}

		Paint.SetPen( c.WithAlphaMultiplied( 0.3f ) );
		Paint.DrawLine( new Vector2( Width * 0.5f, 0 ), new Vector2( Width * 0.5f, Height ) );
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		if ( !e.LeftMouseButton ) return;

		EditMode?.KeyframeDragStart( this, e );
	}

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		if ( !e.LeftMouseButton ) return;

		EditMode?.KeyframeDragMove( this, e );
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		if ( !e.LeftMouseButton ) return;

		EditMode?.KeyframeDragEnd( this, e );
	}

	public int CompareTo( KeyframeHandle? other )
	{
		if ( ReferenceEquals( this, other ) )
		{
			return 0;
		}

		if ( other is null )
		{
			return 1;
		}

		return _keyframe.CompareTo( other.Keyframe );
	}
}
