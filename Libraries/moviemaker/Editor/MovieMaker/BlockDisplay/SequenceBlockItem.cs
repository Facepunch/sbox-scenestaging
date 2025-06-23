using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public sealed class SequenceBlockItem : BlockItem<ProjectSequenceBlock>, IMovieDraggable, IMovieResizable
{
	private RealTimeSince _lastClick;
	private GraphicsItem? _ghost;
	private BlockEdge? _resizeEdge;

	public new ProjectSequenceTrack Track => (ProjectSequenceTrack)Parent.View.Track;

	public string BlockTitle => Block.Resource.ResourceName.ToTitleCase();

	public Rect FullSceneRect
	{
		get
		{
			var fullTimeRange = FullTimeRange.ClampStart( 0d );

			var min = Parent.Session.TimeToPixels( fullTimeRange.Start );
			var max = Parent.Session.TimeToPixels( fullTimeRange.End );

			return SceneRect with { Left = min, Right = max };
		}
	}

	public bool ShowFullTimeRange
	{
		get => _ghost.IsValid();
		set
		{
			if ( _ghost.IsValid() == value ) return;

			if ( !value )
			{
				_ghost?.Destroy();
				_ghost = null;
				return;
			}

			var fullSceneRect = FullSceneRect;

			_ghost = new FullBlockGhostItem();
			_ghost.Position = new Vector2( fullSceneRect.Left, Position.y );
			_ghost.Size = new Vector2( fullSceneRect.Width, Height );
			_ghost.Parent = Parent;
		}
	}

	public SequenceBlockItem()
	{
		HoverEvents = true;
		Selectable = true;

		Cursor = CursorShape.Finger;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		_ghost?.Destroy();
		_ghost = null;
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		if ( Parent.View.IsLocked ) return;
		if ( !e.LeftMouseButton && !e.RightMouseButton ) return;

		// e.Accepted = true;

		var time = Parent.Session.ScenePositionToTime( e.ScenePosition );

		if ( e.RightMouseButton )
		{
			Parent.Session.PlayheadTime = time;
			e.Accepted = true;
		}
	}

	private MovieTimeRange FullTimeRange
	{
		get
		{
			var sourceTimeRange = new MovieTimeRange( 0d, Block.Resource.GetCompiled().Duration );
			return new MovieTimeRange( Block.Transform * sourceTimeRange.Start, Block.Transform * sourceTimeRange.End );
		}
	}

	private void OnSplit( MovieTime time )
	{
		if ( time <= TimeRange.Start ) return;
		if ( time >= TimeRange.End ) return;

		using ( Parent.Session.History.Push( $"Split Sequence ({BlockTitle})" ) )
		{
			Track.AddBlock( (time, Block.TimeRange.End), Block.Transform, Block.Resource );

			Block.TimeRange = (Block.TimeRange.Start, time);
		}

		Layout();
		Parent.View.MarkValueChanged();
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		if ( e.RightMouseButton )
		{
			OnOpenContextMenu( e );
			return;
		}

		if ( !e.LeftMouseButton ) return;

		if ( _lastClick < 0.5f )
		{
			OnEdit();
		}

		_lastClick = 0f;
	}

	private void OnOpenContextMenu( GraphicsMouseEvent e )
	{
		e.Accepted = true;

		Selected = true;

		var time = Parent.Session.PlayheadTime;

		var menu = new Menu();

		menu.AddHeading( "Sequence Block" );

		menu.AddOption( "Edit", "edit", OnEdit );

		if ( time > TimeRange.Start && time < TimeRange.End )
		{
			menu.AddOption( "Split", "carpenter", () => OnSplit( time ) );
		}

		menu.AddOption( "Delete", "delete", OnDelete );

		menu.OpenAt( e.ScreenPosition );
	}

	private void OnDelete()
	{
		using ( Parent.Session.History.Push( "Sequence Deleted" ) )
		{
			Track.RemoveBlock( Block );

			if ( Track.IsEmpty )
			{
				Parent.View.Remove();
			}
		}

		Parent.View.MarkValueChanged();
	}

	private void OnEdit()
	{
		if ( Block.Resource is { } resource )
		{
			Parent.Session.Editor.EnterSequence( resource, Block.Transform, Block.Transform.Inverse * Block.TimeRange );
		}
	}

	protected override void OnPaint()
	{
		var isLocked = Parent.View.IsLocked;
		var isSelected = !isLocked && Selected;
		var isHovered = !isLocked && Paint.HasMouseOver;

		var color = Theme.Primary.Desaturate( isLocked ? 0.25f : 0f ).Darken( isLocked ? 0.5f : isSelected ? 0f : isHovered ? 0.1f : 0.25f );

		PaintExtensions.PaintFilmStrip( LocalRect.Shrink( 0f, 0f, 1f, 0f ), color );

		var minX = LocalRect.Left;
		var maxX = LocalRect.Right;

		Paint.ClearBrush();
		Paint.SetPen( Theme.TextControl.Darken( isLocked ? 0.25f : 0f ) );

		var textRect = new Rect( minX + 8f, LocalRect.Top, maxX - minX - 16f, LocalRect.Height );
		var fullTimeRange = FullTimeRange;

		if ( _resizeEdge is BlockEdge.Start )
		{
			TryDrawText( ref textRect, $"{Block.TimeRange.End - fullTimeRange.Start}", TextFlag.RightCenter );
			TryDrawText( ref textRect, $"{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
		}
		else if ( _resizeEdge is BlockEdge.End )
		{
			TryDrawText( ref textRect, $"{Block.TimeRange.Start - fullTimeRange.Start}", TextFlag.LeftCenter );
			TryDrawText( ref textRect, $"{Block.TimeRange.End - fullTimeRange.Start}", TextFlag.RightCenter );
		}
		else
		{
			TryDrawText( ref textRect, BlockTitle, icon: "movie", flags: TextFlag.LeftCenter );
		}
	}

	private void TryDrawText( ref Rect rect, string text, TextFlag flags = TextFlag.Center, string? icon = null, float iconSize = 16f )
	{
		var originalRect = rect;

		if ( icon != null )
		{
			if ( rect.Width < iconSize ) return;

			rect.Left += iconSize + 4f;
		}

		var textRect = Paint.MeasureText( rect, text, flags );

		if ( textRect.Width > rect.Width )
		{
			if ( icon != null )
			{
				Paint.DrawIcon( originalRect, icon, iconSize, flags );
			}

			rect = default;
			return;
		}

		if ( icon != null )
		{
			Paint.DrawIcon( new Rect( textRect.Left - iconSize - 4f, rect.Top, iconSize, rect.Height ), icon, iconSize, flags );
		}

		Paint.DrawText( rect, text, flags );

		if ( (flags & TextFlag.Left) != 0 )
		{
			rect.Left = textRect.Right;
		}
		else if ( (flags & TextFlag.Right) != 0 )
		{
			rect.Right = textRect.Left;
		}
	}

	ITrackBlock IMovieTrackItem.Block => Block;
	MovieTimeRange IMovieTrackItem.TimeRange => Block.TimeRange;
	MovieTimeRange? IMovieResizable.FullTimeRange => FullTimeRange;

	void IMovieDraggable.Drag( MovieTime delta )
	{
		Block.TimeRange += delta;
		Block.Transform += delta;

		Layout();
	}

	void IMovieResizable.StartResize( BlockEdge edge )
	{
		ShowFullTimeRange = true;

		_resizeEdge = edge;
	}

	void IMovieResizable.Resize( BlockEdge edge, MovieTime delta )
	{
		Block.TimeRange = 
			edge == BlockEdge.Start
				? Block.TimeRange with { Start = Block.TimeRange.Start + delta }
				: Block.TimeRange with { End = Block.TimeRange.End + delta };

		Layout();
	}

	void IMovieResizable.EndResize()
	{
		ShowFullTimeRange = false;

		_resizeEdge = null;
	}
}

file sealed class FullBlockGhostItem : GraphicsItem
{
	public FullBlockGhostItem()
	{
		ZIndex = -100;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Timeline.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect, 2 );
	}
}
