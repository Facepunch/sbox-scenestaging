using Sandbox.MovieMaker;

namespace Editor.MovieMaker.BlockDisplays;

#nullable enable

public abstract partial class BlockItem : GraphicsItem
{
	public new DopeSheetTrack Parent { get; private set; } = null!;
	public IMovieBlock Block { get; internal set; } = null!;

	protected MovieTrack Track => Parent.TrackWidget.MovieTrack;
	protected IMovieBlockData Data => Block.Data;
	protected MovieTimeRange TimeRange => Block.TimeRange;

	protected int DataHash => HashCode.Combine( Data, TimeRange.Duration, Width );

	protected string? DebugText { get; set; }

	private void Initialize( DopeSheetTrack parent, IMovieBlock block )
	{
		base.Parent = Parent = parent;

		Block = block;
		ZIndex = -1;
	}

	protected override void OnPaint()
	{
		var canModify = Track.CanModify();

		Paint.SetBrushAndPen( DopeSheet.Colors.ChannelBackground.Lighten( Track.CanModify() ? 0f : 0.2f ) );
		Paint.DrawRect( LocalRect );

		if ( !canModify ) return;

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
	public ConstantData<T>? Constant => Data as ConstantData<T>;
	public SamplesData<T>? Samples => Data as SamplesData<T>;
}
