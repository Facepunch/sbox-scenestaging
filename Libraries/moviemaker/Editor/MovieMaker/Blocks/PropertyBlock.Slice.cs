using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDerivedType( typeof(PropertyBlockSlice<>), "Slice" )]
partial record PropertyBlock<T>
{
	public PropertyBlock<T> Slice( MovieTimeRange timeRange )
	{
		return timeRange == TimeRange ? this : new PropertyBlockSlice<T>( this, TimeRange, 0d ).Reduce();
	}

	public PropertyBlock<T> Shift( MovieTime offset )
	{
		return offset == default ? this : new PropertyBlockSlice<T>( this, TimeRange + offset, offset ).Reduce();
	}

	IProjectPropertyBlock IProjectPropertyBlock.Slice( MovieTimeRange timeRange ) => Slice( timeRange );
	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => Shift( offset );
}

file sealed record PropertyBlockSlice<T>( PropertyBlock<T> Block, MovieTimeRange TimeRange, MovieTime Offset = default )
	: PropertyBlock<T>( TimeRange )
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time.Clamp( TimeRange ) - Offset );

	protected override PropertyBlock<T> OnReduce()
	{
		// Can strip slice if it doesn't do anything

		if ( Offset == default && Block.TimeRange == TimeRange )
		{
			return Block;
		}

		// Avoid nested slices if we're contained within a parent slice

		if ( Block is PropertyBlockSlice<T> parentSlice && parentSlice.TimeRange.Contains( TimeRange + Offset ) )
		{
			return parentSlice with
			{
				TimeRange = TimeRange,
				Offset = parentSlice.Offset + Offset
			};
		}

		// Constant blocks can be directly sliced

		if ( Block is ConstantPropertyBlock<T> constantBlock )
		{
			return constantBlock with { TimeRange = TimeRange };
		}

		return this;
	}

	protected override PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next )
	{
		if ( next is not PropertyBlockSlice<T> nextSlice ) return null;

		if ( Block != nextSlice.Block ) return null;
		if ( Offset != nextSlice.Offset ) return null;

		return new PropertyBlockSlice<T>( Block, (TimeRange.Start, next.TimeRange.End), Offset );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return !Offset.IsZero
			? Block.GetPaintHintTimes( timeRange - Offset ).Select( x => x + Offset )
			: Block.GetPaintHintTimes( timeRange );
	}

	public override string ToString()
	{
		return $"Slice {{ Block = {Block}, TimeRange = {TimeRange}, Offset = {Offset} }}";
	}
}
