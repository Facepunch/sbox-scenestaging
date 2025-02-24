using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

public sealed record PropertyBlockSlice<T>( IPropertyBlock<T> Block, MovieTimeRange TimeRange, MovieTime Offset ) : IPropertyBlock<T>
{
	public Type PropertyType => Block.PropertyType;

	public T GetValue( MovieTime time ) => Block.GetValue( time - Offset );
	object? IPropertyBlock.GetValue( MovieTime time ) => GetValue( time );
}

public static class EditExtensions
{
	public static IPropertyBlock<T> Slice<T>( this IPropertyBlock<T> block, MovieTimeRange timeRange )
	{
		var offset = timeRange.Start - block.TimeRange.Start;

		return block is PropertyBlockSlice<T> slice
			? new PropertyBlockSlice<T>( slice.Block, timeRange, offset + slice.Offset )
			: new PropertyBlockSlice<T>( block, timeRange, offset );
	}

	public static IPropertyBlock<T> Shift<T>( this IPropertyBlock<T> block, MovieTime offset )
	{
		var timeRange = block.TimeRange + offset;

		return block is PropertyBlockSlice<T> slice
			? new PropertyBlockSlice<T>( slice.Block, timeRange, offset + slice.Offset )
			: new PropertyBlockSlice<T>( block, timeRange, offset );
	}
}
