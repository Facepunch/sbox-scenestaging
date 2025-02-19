using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockPreviews;

#nullable enable

public abstract partial class BlockPreview : GraphicsItem
{
	public new DopeSheetTrack Parent { get; private set; } = null!;
	public MovieBlock Block { get; set; } = null!;

	protected MovieTrack Track => Block.Track;
	protected MovieBlockData Data => Block.Data;
	protected float StartTime => Block.StartTime;
	protected float Duration => Block.Duration;

	protected int DataHash => HashCode.Combine( Block.Data, Block.Duration );

	private void Initialize( DopeSheetTrack parent, MovieBlock block )
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
