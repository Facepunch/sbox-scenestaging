using System.Text.Json;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public abstract partial class BlockItem : GraphicsItem
{
	private IPropertyBlock? _block;

	public new DopeSheetTrack Parent { get; private set; } = null!;

	public IPropertyBlock Block
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

			try
			{
				ToolTip = value?.ToString();
			}
			catch ( Exception ex )
			{
				Log.Error( ex );
			}

			if ( _block is IDynamicBlock newBlock )
			{
				newBlock.Changed += Block_Changed;
			}
		}
	}

	protected IProjectTrack Track => Parent.TrackWidget.ProjectTrack;
	protected MovieTimeRange TimeRange => Block.TimeRange;

	protected int DataHash => HashCode.Combine( Block, TimeRange.Duration, Width );

	protected string? DebugText { get; set; }

	private void Initialize( DopeSheetTrack parent, IPropertyBlock block )
	{
		base.Parent = Parent = parent;

		Block = block;
		ZIndex = -1;
	}

	private void Block_Changed() => Layout();

	protected override void OnDestroy()
	{
		// To remove Changed event

		Block = null!;
	}

	public void Layout()
	{
		var session = Parent.TrackWidget.Session;

		PrepareGeometryChange();

		Position = new Vector2( session.TimeToPixels( TimeRange.Start ), 0f );
		Size = new Vector2( session.TimeToPixels( TimeRange.Duration ), Parent.Height );

		Update();
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( DopeSheet.Colors.ChannelBackground.Lighten( Parent.TrackWidget.IsLocked ? 0.2f : 0f ) );
		Paint.DrawRect( LocalRect );

		if ( Parent.TrackWidget.IsLocked ) return;

		Paint.ClearBrush();
		Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
		Paint.DrawLine( LocalRect.BottomLeft, LocalRect.TopLeft );
		Paint.DrawLine( LocalRect.BottomRight, LocalRect.TopRight );

		if ( DebugText is { } debugText )
		{
			Paint.SetPen( Color.White );
			Paint.DrawText( LocalRect.TopLeft, debugText );
		}
	}
}

internal interface IBlockItem<T>;

public abstract class BlockItem<T> : BlockItem, IBlockItem<T>
{
	public new IPropertyBlock<T> Block => (IPropertyBlock<T>)base.Block;
}
