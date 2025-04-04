using System.Linq;
using Editor.MovieMaker.BlockDisplays;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public partial class TimelineTrack : GraphicsItem
{
	// TODO: should this be in DisplayInfo?
	private static Dictionary<Type, Color> HandleColors { get; } = new()
	{
		{ typeof(Vector3), Theme.Blue },
		{ typeof(Rotation), Theme.Green },
		{ typeof(Color), Theme.Pink },
		{ typeof(float), Theme.Yellow },
	};

	public Timeline Timeline { get; }
	public Session Session { get; }
	public TrackView View { get; }

	private readonly record struct BlockItemKey( ITrackBlock Block, MovieTime? Offset = null ) : IEquatable<BlockItemKey>
	{
		public bool Equals( BlockItemKey other )
		{
			return ReferenceEqualityComparer.Instance.Equals( Block, other.Block ) && Offset == other.Offset;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine( ReferenceEqualityComparer.Instance.GetHashCode( Block ), Offset );
		}
	}

	private readonly List<BlockItemKey> _visibleBlocks = new();
	private readonly SynchronizedList<BlockItemKey, BlockItem> _blockItems;

	public IReadOnlyList<BlockItem> BlockItems => _blockItems;

	public Color HandleColor { get; }

	public TimelineTrack( Timeline timeline, TrackView view )
	{
		Timeline = timeline;
		Session = timeline.Session;
		View = view;

		HoverEvents = true;
		ToolTip = view.Description;

		HandleColor = HandleColors.TryGetValue( view.Track.TargetType, out var color ) ? color : Theme.Grey;

		_blockItems = new SynchronizedList<BlockItemKey, BlockItem>(
			AddBlockItem, RemoveBlockItem, UpdateBlockItem );

		View.Changed += View_Changed;
		View.ValueChanged += View_ValueChanged;
	}

	protected override void OnDestroy()
	{
		Session.EditMode?.ClearTimelineItems( this );

		View.Changed -= View_Changed;
		View.ValueChanged -= View_ValueChanged;
	}

	private void View_Changed( TrackView view )
	{
		UpdateItems();
	}

	private void View_ValueChanged( TrackView view )
	{
		UpdateItems();
	}

	internal void UpdateLayout()
	{
		PrepareGeometryChange();

		var position = View.Position;

		Position = new Vector2( 0, position );
		Size = new Vector2( 50000, Timeline.TrackHeight );

		UpdateItems();
	}

	internal void OnSelected()
	{
		View.InspectProperty();
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		if ( e.LeftMouseButton )
		{
			OnSelected();
		}
	}

	public void UpdateItems()
	{
		var previewOffset = Session.TrackList.PreviewOffset;

		_visibleBlocks.Clear();
		_visibleBlocks.AddRange( View.Blocks.Select( x => new BlockItemKey( x ) ) );
		_visibleBlocks.AddRange( View.PreviewBlocks.Select( x => new BlockItemKey( x, previewOffset ) ) );

		_blockItems.Update( _visibleBlocks );

		Session.EditMode?.UpdateTimelineItems( this );
	}

	private BlockItem AddBlockItem( BlockItemKey src ) => BlockItem.Create( this, src.Block, src.Offset ?? default );

	private void RemoveBlockItem( BlockItem dst ) => dst.Destroy();

	private bool UpdateBlockItem( BlockItemKey src, ref BlockItem dst )
	{
		if ( dst.Block.GetType() != src.Block.GetType() )
		{
			dst = AddBlockItem( src );
		}

		dst.Block = src.Block;
		dst.Offset = src.Offset ?? default;
		dst.Layout();

		return true;
	}
}
