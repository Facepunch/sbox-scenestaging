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
	private MovieTime? _maxDragTime;
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

		_dragSnapOptions = new SnapOptions(
			IgnoreBlocks: _draggedItems.Select( x => x.Block )
				.OfType<ITrackBlock>()
				.ToImmutableHashSet(),
			SnapOffsets: _draggedItems.Select( x => x.TimeRange.Start - _lastDragTime )
				.Concat( _draggedItems.Select( x => x.TimeRange.End - _lastDragTime ) )
				.Order().Distinct().ToArray() );

		_minDragTime = -_dragSnapOptions.SnapOffsets[0];
		_maxDragTime = null;

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

		_dragSnapOptions = new SnapOptions(
			IgnoreBlocks: _resizedItems.Select( x => x.Item.Block )
				.OfType<ITrackBlock>()
				.ToImmutableHashSet() );

		var min = MovieTime.Zero;
		var max = MovieTime.MaxValue;

		foreach ( var resized in _resizedItems )
		{
			if ( resized.Item.FullTimeRange is { } fullRange )
			{
				min = MovieTime.Max( min, fullRange.Start );
				max = MovieTime.Min( max, fullRange.End );
			}

			if ( resized.Edge == BlockEdge.Start )
			{
				max = MovieTime.Min( max, resized.Item.TimeRange.End );
			}
			else
			{
				min = MovieTime.Max( min, resized.Item.TimeRange.Start );
			}
		}

		_minDragTime = min;
		_maxDragTime = max;

		return _resizedItems.Count > 0;
	}

	private void Drag( Vector2 scenePos )
	{
		var time = MovieTime.Max( _minDragTime, Session.ScenePositionToTime( scenePos, _dragSnapOptions ) );

		if ( _maxDragTime is { } maxTime )
		{
			time = MovieTime.Min( time, maxTime );
		}

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
		_resizedItems.Clear();

		Cursor = CursorShape.Arrow;
	}
}
