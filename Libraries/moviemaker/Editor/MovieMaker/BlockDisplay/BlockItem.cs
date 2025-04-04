using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public abstract partial class BlockItem : GraphicsItem
{
	private ITrackBlock? _block;

	public new TimelineTrack Parent { get; private set; } = null!;

	public ITrackBlock Block
	{
		get => _block ?? throw new InvalidOperationException();
		set
		{
			if ( ReferenceEquals( _block, value ) ) return;

			if ( _block is IDynamicBlock oldBlock )
			{
				oldBlock.Changed -= Block_Changed;
			}

			_block = value;

			if ( _block is IDynamicBlock newBlock )
			{
				newBlock.Changed += Block_Changed;
			}
		}
	}

	public MovieTime Offset { get; set; }

	protected IProjectTrack Track => Parent.View.Track;
	protected MovieTimeRange TimeRange => Block.TimeRange + Offset;

	protected int DataHash => HashCode.Combine( Block, TimeRange.Duration, Width );

	private void Initialize( TimelineTrack parent, ITrackBlock block, MovieTime offset )
	{
		base.Parent = Parent = parent;

		Block = block;
		Offset = offset;
	}

	private void Block_Changed() => Layout();

	protected override void OnDestroy()
	{
		// To remove Changed event

		Block = null!;
	}

	public void Layout()
	{
		var session = Parent.Session;

		PrepareGeometryChange();

		Position = new Vector2( session.TimeToPixels( TimeRange.Start ), 1f );
		Size = new Vector2( session.TimeToPixels( TimeRange.Duration ), Parent.Height - 2f );

		Update();
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( Timeline.Colors.ChannelBackground.Lighten( Parent.View.IsLocked ? 0.2f : 0f ) );
		Paint.DrawRect( LocalRect );

		if ( Parent.View.IsLocked ) return;

		Paint.ClearBrush();
		Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
		Paint.DrawLine( LocalRect.BottomLeft, LocalRect.TopLeft );
		Paint.DrawLine( LocalRect.BottomRight, LocalRect.TopRight );
	}
}

public interface IBlockItem<T>;

public abstract class BlockItem<T> : BlockItem, IBlockItem<T>
	where T : ITrackBlock
{
	public new T Block => (T)base.Block;
}

public interface IPropertyBlockItem<T>;

public abstract class PropertyBlockItem<T> : BlockItem<IPropertyBlock<T>>, IPropertyBlockItem<T>;
