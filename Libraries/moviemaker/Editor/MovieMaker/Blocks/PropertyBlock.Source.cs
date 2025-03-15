using System.Linq;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;
using System.Text.Json.Serialization;

namespace Editor.MovieMaker;

#nullable enable

partial class PropertyBlock<T>
{
	public static PropertyBlock<T> SourceClip( ProjectSourceClip source, CompiledPropertyTrack<T> track, CompiledPropertyBlock<T> block )
	{
		if ( block is CompiledConstantBlock<T> constant )
		{
			return Constant( constant.Value, block.TimeRange );
		}

		return new SourceClipPropertyBlock<T>( source, track, block );
	}
}

[JsonDiscriminator( "Clip" )]
file sealed class SourceClipPropertyBlock<T>( ProjectSourceClip source, CompiledPropertyTrack<T> track, CompiledPropertyBlock<T> block )
	: PropertyBlock<T>( block.TimeRange )
{
	public ProjectSourceClip Source { get; } = source;
	public CompiledPropertyTrack<T> Track { get; } = track;
	public CompiledPropertyBlock<T> Block { get; } = block;

	protected override T OnGetValue( MovieTime time ) => Block.GetValue( time );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return new SourcePropertyBlockSlice<T>( this, timeRange, MovieTime.Zero );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return new SourcePropertyBlockSlice<T>( this, TimeRange + offset, offset );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		switch ( Block )
		{
			case CompiledSampleBlock<T> sampleBlock:
				return IPropertyBlock.GetSampleTimes( timeRange, sampleBlock.TimeRange.Start + sampleBlock.Offset,
					sampleBlock.Samples.Length, sampleBlock.SampleRate );

			default:
				return [];
		}
	}

	protected override int OnGetHashCode()
	{
		return Block.GetHashCode();
	}

	protected override bool EqualsBlock( PropertyBlock<T> other )
	{
		return other is SourceClipPropertyBlock<T> sourceClipBlock
			&& Block.Equals( sourceClipBlock.Block );
	}
}

[JsonDiscriminator( "Slice" )]
file sealed class SourcePropertyBlockSlice<T>( SourceClipPropertyBlock<T> block, MovieTimeRange timeRange, MovieTime offset )
	: PropertyBlock<T>( timeRange )
{
	[JsonInclude] public new MovieTimeRange TimeRange => base.TimeRange;

	[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )]
	public MovieTime Offset { get; } = offset;

	public SourceClipPropertyBlock<T> Block { get; } = block;

	protected override T OnGetValue( MovieTime time ) => Block.GetValue( time - Offset );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return new SourcePropertyBlockSlice<T>( Block, timeRange, Offset );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return new SourcePropertyBlockSlice<T>( Block, TimeRange + offset, Offset + offset );
	}

	protected override PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next )
	{
		if ( next is not SourcePropertyBlockSlice<T> nextSlice ) return null;

		if ( !Block.Equals( nextSlice.Block ) ) return null;
		if ( Offset != nextSlice.Offset ) return null;

		return new SourcePropertyBlockSlice<T>( Block, TimeRange.Union( nextSlice.TimeRange ), Offset );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return Offset is { IsZero: false } offset
			? Block.GetPaintHintTimes( timeRange - offset ).Select( x => x + offset )
			: Block.GetPaintHintTimes( timeRange );
	}

	protected override int OnGetHashCode()
	{
		return HashCode.Combine( Block, TimeRange, Offset );
	}

	protected override bool EqualsBlock( PropertyBlock<T> other )
	{
		return other is SourcePropertyBlockSlice<T> slice
			&& slice.Block.Equals( Block )
			&& slice.TimeRange == TimeRange
			&& slice.Offset == Offset;
	}
}
