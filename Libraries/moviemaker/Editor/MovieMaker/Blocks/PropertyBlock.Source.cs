using System.Linq;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;
using System.Text.Json.Serialization;

namespace Editor.MovieMaker;

#nullable enable

partial class PropertyBlock<T>
{
	public static PropertyBlock<T> Constant( T value ) => new ConstantPropertyBlock<T>( value );
	public static PropertyBlock<T> SourceClip( ProjectSourceClip source, CompiledPropertyTrack<T> track, CompiledPropertyBlock<T> block )
		=> new SourceClipPropertyBlock<T>( source, track, block );
}

/// <summary>
/// A <see cref="PropertyBlock{T}"/> that contains source data, like a constant or a reference to an external clip.
/// </summary>
[JsonConverter( typeof(PropertyBlockConverterFactory) )]
file abstract class SourcePropertyBlock<T>( MovieTimeRange timeRange ) : PropertyBlock<T>( timeRange );

[JsonDiscriminator( "Constant" )]
file sealed class ConstantPropertyBlock<T>( T value ) : SourcePropertyBlock<T>( default )
{
	public T Value { get; } = value;

	public override T GetValue( MovieTime time ) => Value;

	public override CompiledPropertyBlock<T> Compile( ProjectTrack track ) =>
		new CompiledConstantBlock<T>( TimeRange, Value );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return new SourcePropertyBlockSlice<T>( this, timeRange );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return new SourcePropertyBlockSlice<T>( this, TimeRange + offset );
	}

	protected override PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next )
	{
		if ( next is not ConstantPropertyBlock<T> nextConstant ) return null;

		return EqualityComparer<T>.Default.Equals( Value, nextConstant.Value ) ? this : null;
	}
}

[JsonDiscriminator( "Clip" )]
file sealed class SourceClipPropertyBlock<T>( ProjectSourceClip source, CompiledPropertyTrack<T> track, CompiledPropertyBlock<T> block )
	: SourcePropertyBlock<T>( block.TimeRange )
{
	public ProjectSourceClip Source { get; } = source;
	public CompiledPropertyTrack<T> Track { get; } = track;
	public CompiledPropertyBlock<T> Block { get; } = block;

	public override T GetValue( MovieTime time ) => Block.GetValue( time );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return new SourcePropertyBlockSlice<T>( this, timeRange, TimeRange, MovieTime.Zero );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return new SourcePropertyBlockSlice<T>( this, TimeRange + offset, TimeRange, offset );
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
}

[JsonDiscriminator( "Slice" )]
file sealed class SourcePropertyBlockSlice<T>( SourcePropertyBlock<T> block,
	MovieTimeRange timeRange,
	MovieTimeRange? sourceTimeRange = null,
	MovieTime? offset = null )
	: PropertyBlock<T>( timeRange )
{
	[JsonInclude] public new MovieTimeRange TimeRange => base.TimeRange;

	[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public MovieTimeRange? SourceTimeRange { get; } = sourceTimeRange;

	[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull )]
	public MovieTime? Offset { get; } = offset;

	public SourcePropertyBlock<T> Block { get; } = block;

	public override T GetValue( MovieTime time ) => Block.GetValue( (time - (Offset ?? MovieTime.Zero)).Clamp( SourceTimeRange ) );

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return new SourcePropertyBlockSlice<T>( Block, timeRange, SourceTimeRange?.Clamp( timeRange - (Offset ?? MovieTime.Zero) ), Offset );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return new SourcePropertyBlockSlice<T>( Block, TimeRange + offset, SourceTimeRange, Offset + offset );
	}

	protected override PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next )
	{
		if ( next is not SourcePropertyBlockSlice<T> nextSlice ) return null;

		if ( Block != nextSlice.Block ) return null;
		if ( Offset != nextSlice.Offset ) return null;
		if ( SourceTimeRange?.End < nextSlice.SourceTimeRange?.Start ) return null;

		return new SourcePropertyBlockSlice<T>( Block, TimeRange.Union( nextSlice.TimeRange ), SourceTimeRange?.Union( nextSlice.SourceTimeRange ), Offset );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return Offset is { IsZero: false } offset
			? Block.GetPaintHintTimes( (timeRange - offset).Clamp( SourceTimeRange ) ).Select( x => x + offset )
			: Block.GetPaintHintTimes( timeRange.Clamp( SourceTimeRange ) );
	}
}
