using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockPreviews;

#nullable enable


public abstract partial class BlockPreview : GraphicsItem
{
	public new DopeSheetTrack Parent { get; private set; } = null!;

	public IMovieBlock Block { get; internal set; }

	protected MovieTrack Track => Parent.TrackWidget.MovieTrack;
	protected IMovieBlockData Data => Block.Data;
	protected MovieTimeRange TimeRange => Block.TimeRange;

	protected int DataHash => HashCode.Combine( Data, TimeRange.Duration, Width );

	private void Initialize( DopeSheetTrack parent, IMovieBlock block )
	{
		base.Parent = Parent = parent;

		Block = block;
		ZIndex = -1;
	}

	protected override void OnPaint()
	{
		Paint.SetBrushAndPen( DopeSheet.Colors.ChannelBackground );
		Paint.DrawRect( LocalRect );

		Paint.ClearBrush();
		Paint.SetPen( Color.White.WithAlpha( 0.1f ) );
		Paint.DrawLine( LocalRect.BottomLeft, LocalRect.TopLeft );
		Paint.DrawLine( LocalRect.BottomRight, LocalRect.TopRight );
	}
}

internal interface IBlockPreview<T>;

public abstract class BlockPreview<T> : BlockPreview, IBlockPreview<T>
{
	public ConstantData<T>? Constant => Data as ConstantData<T>;
	public SamplesData<T>? Samples => Data as SamplesData<T>;
}
