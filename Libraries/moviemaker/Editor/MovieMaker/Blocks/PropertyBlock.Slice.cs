using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertyBlock<T>
{
	public PropertyBlock<T> Slice( MovieTimeRange timeRange ) => timeRange == TimeRange ? this : OnSlice( timeRange );
	protected virtual PropertyBlock<T> OnSlice( MovieTimeRange timeRange ) => new PropertyBlockSlice<T>( this, timeRange, 0d ).Reduce();

	public PropertyBlock<T> Shift( MovieTime offset ) => offset == default ? this : OnShift( offset );
	protected virtual PropertyBlock<T> OnShift( MovieTime offset ) => new PropertyBlockSlice<T>( this, TimeRange + offset, offset ).Reduce();

	IProjectPropertyBlock IProjectPropertyBlock.Slice( MovieTimeRange timeRange ) => Slice( timeRange );
	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => Shift( offset );
}

[JsonDiscriminator( "Slice" )]
file sealed record PropertyBlockSlice<T>( PropertyBlock<T> Block, MovieTimeRange TimeRange,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] MovieTime Offset = default )
	: PropertyBlock<T>( TimeRange )
{
	[JsonInclude] public new MovieTimeRange TimeRange { get => base.TimeRange; init => base.TimeRange = value; }

	public override T GetValue( MovieTime time ) => Block.GetValue( time.Clamp( TimeRange ) - Offset );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		var withinSameRange = TimeRange.Contains( timeRange )
			|| TimeRange.Start >= Block.TimeRange.End && timeRange.Start >= Block.TimeRange.End
			|| TimeRange.End <= Block.TimeRange.Start && timeRange.End <= Block.TimeRange.Start;

		return withinSameRange
			? this with { TimeRange = timeRange }
			: base.OnSlice( timeRange );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return this with { TimeRange = TimeRange + offset, Offset = offset + Offset };
	}

	protected override PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next )
	{
		if ( next is not PropertyBlockSlice<T> nextSlice ) return null;

		if ( Block != nextSlice.Block ) return null;
		if ( Offset != nextSlice.Offset ) return null;

		return Block.Shift( Offset ).Slice( TimeRange with { End = next.TimeRange.End } );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return !Offset.IsZero
			? Block.GetPaintHintTimes( timeRange - Offset ).Select( x => x + Offset )
			: Block.GetPaintHintTimes( timeRange );
	}
}
