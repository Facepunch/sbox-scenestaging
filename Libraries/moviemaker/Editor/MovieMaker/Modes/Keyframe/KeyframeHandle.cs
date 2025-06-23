
using Editor.MapEditor;
using Sandbox.MovieMaker;
using System.Collections.Immutable;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public sealed class KeyframeHandle : GraphicsItem, IComparable<KeyframeHandle>, IMovieDraggable
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

		ZIndex = Selected ? 101 : 100;
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
		base.OnMousePressed( e );

		e.Accepted = true;
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		base.OnMouseReleased( e );

		Session.PlayheadTime = Time;

		if ( e.RightMouseButton )
		{
			ShowContextMenu();
		}

		e.Accepted = true;
	}

	private void ShowContextMenu()
	{
		if ( EditMode is not { } editMode ) return;

		var menu = new Menu();

		Selected = true;
		editMode.Session.PlayheadTime = Keyframe.Time;

		var selection = GraphicsView.SelectedItems
			.OfType<KeyframeHandle>()
			.ToImmutableArray();

		menu.AddHeading( $"Selected Keyframe{(selection.Length > 1 ? "s" : "")}" );

		CreateInterpolationMenu( selection, menu );

		menu.AddSeparator();

		menu.AddOption( "Copy", "content_copy", () => editMode.Copy() );
		menu.AddOption( "Cut", "content_cut", () => editMode.Cut() );
		menu.AddOption( "Delete", "delete", () => editMode.Delete() );

		menu.OpenAtCursor();
	}

	private void CreateInterpolationMenu( IReadOnlyList<KeyframeHandle> selection, Menu parent )
	{
		var menu = parent.AddMenu( "Interpolation Mode", "gradient" );
		var currentMode = selection.All( x => x.Keyframe.Interpolation == selection[0].Keyframe.Interpolation )
			? selection[0].Keyframe.Interpolation
			: KeyframeInterpolation.Unknown;

		foreach ( var value in Enum.GetValues<KeyframeInterpolation>() )
		{
			if ( value < 0 ) continue;

			var option = menu.AddOption( value.ToString().ToTitleCase(), action: () =>
			{
				foreach ( var handle in selection )
				{
					handle.Keyframe = handle.Keyframe with { Interpolation = value };
				}

				EditMode?.UpdateTracksFromHandles( selection );
			} );

			option.Checkable = true;
			option.Checked = value == currentMode;
		}
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

		var timeCompare = Time.CompareTo( other.Time );

		if ( timeCompare != 0 )
		{
			return timeCompare;
		}

		// When overlapping, put selected first

		return -Selected.CompareTo( other.Selected );
	}

	ITrackBlock? IMovieTrackItem.Block => null;

	MovieTimeRange IMovieTrackItem.TimeRange => Keyframe.Time;

	void IMovieDraggable.Drag( MovieTime delta )
	{
		Time += delta;
	}
}
