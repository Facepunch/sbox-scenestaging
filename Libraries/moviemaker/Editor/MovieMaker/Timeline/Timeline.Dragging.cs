using System.Collections.Immutable;
using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public interface IMovieTrackItem
{
	ITrackBlock? Block { get; }
	MovieTimeRange TimeRange { get; }
}

public interface IMovieDraggable : IMovieTrackItem
{
	void Drag( MovieTime delta );
}

public enum BlockEdge
{
	Start,
	End
}

public interface IMovieResizable : IMovieTrackItem
{
	MovieTimeRange? FullTimeRange { get; }

	void Drag( BlockEdge edge, MovieTime delta );
}

partial class Timeline
{
	private readonly List<IMovieDraggable> _draggedItems = new();
	private readonly List<(IMovieResizable Item, BlockEdge Edge)> _resizedItems = new();
	private MovieTime _lastDragTime;
	private MovieTime _minDragTime;
	private SnapOptions _dragSnapOptions;
	private IHistoryScope? _dragScope;

	public bool IsDragging => _draggedItems.Count > 0 || _resizedItems.Count > 0;

	public Rect GetSceneRect( GraphicsItem item, MovieTimeRange timeRange )
	{
		timeRange = timeRange.ClampStart( 0d );

		var min = Session.TimeToPixels( timeRange.Start );
		var max = Session.TimeToPixels( timeRange.End );

		return item.SceneRect with { Left = min, Right = max };
	}

	private BlockEdge? GetBlockEdge( Vector2 scenePos, GraphicsItem item )
	{
		var leftMax = Math.Min( item.SceneRect.Left + 8f, item.Center.x );
		var rightMin = Math.Max( item.SceneRect.Right - 8f, item.Center.x );

		if ( scenePos.x < leftMax )
		{
			return BlockEdge.Start;
		}

		if ( scenePos.x > rightMin )
		{
			return BlockEdge.End;
		}

		return null;
	}

	private void UpdateCursor( Vector2 scenePos, GraphicsItem item )
	{
		if ( item is IMovieDraggable )
		{
			item.Cursor = CursorShape.Finger;
		}

		if ( item is IMovieResizable && GetBlockEdge( scenePos, item ) is not null )
		{
			item.Cursor = CursorShape.SizeH;
		}
	}

	private bool StartDragging( Vector2 scenePos, GraphicsItem item )
	{
		_dragScope = null;
		_lastDragTime = Session.ScenePositionToTime( scenePos, new SnapOptions( SnapFlag.TrackBlock ) );

		_draggedItems.Clear();
		_resizedItems.Clear();

		if ( StartResizing( scenePos, item ) ) return true;

		if ( item is not IMovieDraggable )
		{
			return false;
		}

		if ( !item.Selected )
		{
			DeselectAll();
			item.Selected = true;
		}

		_draggedItems.AddRange( SelectedItems.OfType<IMovieDraggable>() );

		PostStartDragging( _draggedItems );

		_backgroundItem.Cursor = CursorShape.Finger;

		return _draggedItems.Count > 0;
	}

	private bool StartResizing( Vector2 scenePos, GraphicsItem item )
	{
		if ( item is not IMovieResizable resizable )
		{
			return false;
		}

		if ( GetBlockEdge( scenePos, item ) is not { } edge )
		{
			return false;
		}

		if ( !item.Selected )
		{
			DeselectAll();
			item.Selected = true;
		}

		_lastDragTime = edge == BlockEdge.Start ? resizable.TimeRange.Start : resizable.TimeRange.End;

		_resizedItems.AddRange( SelectedItems.OfType<IMovieResizable>()
			.Where( x => x.TimeRange.Start == _lastDragTime || x.TimeRange.End == _lastDragTime )
			.Select( x => (x, x.TimeRange.Start == _lastDragTime ? BlockEdge.Start : BlockEdge.End) ) );

		PostStartDragging( _resizedItems.Select( x => x.Item ) );

		_backgroundItem.Cursor = CursorShape.SizeH;

		return _resizedItems.Count > 0;
	}

	private void PostStartDragging( IEnumerable<IMovieTrackItem> items )
	{
		var ignoreBlocks = new HashSet<ITrackBlock>();
		var snapOffsets = new HashSet<MovieTime>();

		foreach ( var item in items )
		{
			if ( item.Block is { } block )
			{
				ignoreBlocks.Add( block );
			}

			snapOffsets.Add( item.TimeRange.Start - _lastDragTime );
			snapOffsets.Add( item.TimeRange.End - _lastDragTime );
		}

		_dragSnapOptions = new SnapOptions( IgnoreBlocks: ignoreBlocks, SnapOffsets: snapOffsets.Order().ToArray() );
		_minDragTime = -_dragSnapOptions.SnapOffsets[0];
	}

	private void Drag( Vector2 scenePos )
	{
		var time = MovieTime.Max( _minDragTime, Session.ScenePositionToTime( scenePos, _dragSnapOptions ) );
		var delta = time - _lastDragTime;

		if ( delta.IsZero ) return;

		_lastDragTime = time;

		var isResizing = _resizedItems.Count > 0;

		_dragScope ??= Session.History.Push( isResizing ? "Resize Selection" : "Drag Selection" );

		if ( isResizing )
		{
			foreach ( var (item, edge) in _resizedItems )
			{
				item.Drag( edge, delta );
			}
		}
		else
		{
			foreach ( var item in _draggedItems )
			{
				item.Drag( delta );
			}
		}

		Session.EditMode?.DragItems( _draggedItems, delta );

		_dragScope?.PostChange();
	}

	private void StopDragging()
	{
		_dragScope?.Dispose();
		_draggedItems.Clear();

		Cursor = CursorShape.Arrow;
	}
}
