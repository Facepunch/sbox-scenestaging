using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public abstract record PropertyBlock( MovieTimeRange TimeRange, Type PropertyType )
{
	public object? GetValue( MovieTime time ) => OnGetValue( time );

	protected abstract object? OnGetValue( MovieTime time );

	public PropertyBlock Slice( MovieTimeRange timeRange ) => OnSlice( timeRange );
	protected abstract PropertyBlock OnSlice( MovieTimeRange timeRange );

	public PropertyBlock Shift( MovieTime offset ) => OnShift( offset );
	protected abstract PropertyBlock OnShift( MovieTime offset );

	public PropertyBlock Stitch( PropertyBlock next ) => OnStitch( next );
	protected abstract PropertyBlock OnStitch( PropertyBlock next );

	/// <summary>
	/// For painting a curve of this property block's value, gets
	/// times that should have a vertex because of a discontinuity.
	/// </summary>
	public IEnumerable<MovieTime> GetPaintHintTimes() => OnGetPaintHintTimes();

	/// <inheritdoc cref="GetPaintHintTimes"/>
	protected virtual IEnumerable<MovieTime> OnGetPaintHintTimes()
	{
		yield return TimeRange.Start;
		yield return TimeRange.End;
	}
}

public abstract record PropertyBlock<T>( MovieTimeRange TimeRange ) : PropertyBlock( TimeRange, typeof(T) )
{
	public new abstract T GetValue( MovieTime time );

	public new virtual PropertyBlock<T> Slice( MovieTimeRange timeRange ) => new PropertyBlockSlice<T>( this, TimeRange, 0d );
	public new virtual PropertyBlock<T> Shift( MovieTime offset ) => new PropertyBlockSlice<T>( this, TimeRange + offset, offset );
	public virtual PropertyBlock<T> Stitch( PropertyBlock<T> next ) => new PropertyBlockStitch<T>( [this, next] );

	protected sealed override object? OnGetValue( MovieTime time ) => GetValue( time );
	protected sealed override PropertyBlock OnSlice( MovieTimeRange timeRange ) => Slice( timeRange );
	protected sealed override PropertyBlock OnShift( MovieTime offset ) => Shift( offset );
	protected sealed override PropertyBlock OnStitch( PropertyBlock next ) => Stitch( (PropertyBlock<T>)next );
}

public sealed record SourceClipPropertyBlock<T>( ProjectSourceClip Source, CompiledPropertyTrack<T> Track, PropertyBlock<T> Block )
	: PropertyBlock<T>( Block.TimeRange )
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time );
}

public sealed record PropertyBlockSlice<T>( PropertyBlock<T> Block, MovieTimeRange TimeRange, MovieTime Offset = default )
	: PropertyBlock<T>( TimeRange )
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time - Offset );

	public override PropertyBlock<T> Slice( MovieTimeRange timeRange )
	{
		return this with { TimeRange = timeRange, Offset = Offset };
	}

	public override PropertyBlock<T> Shift( MovieTime offset )
	{
		return this with { Offset = Offset + offset };
	}
}

public sealed record PropertyBlockStitch<T>( ImmutableArray<PropertyBlock<T>> Blocks )
	: PropertyBlock<T>( (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End) )
{
	private readonly bool _validated = Validate( Blocks );

	public override PropertyBlock<T> Stitch( PropertyBlock<T> next ) => new PropertyBlockStitch<T>( [..Blocks, next] );

	public override T GetValue( MovieTime time )
	{
		if ( time < TimeRange.Start ) return Blocks[0].GetValue( time );
		if ( time > TimeRange.End ) return Blocks[^1].GetValue( time );

		for ( var i = Blocks.Length - 1; i >= 0; --i )
		{
			if ( Blocks[i].TimeRange.Contains( time ) ) return Blocks[i].GetValue( time );
		}

		throw new Exception( "Expected blocks to be connected." );
	}

	private static bool Validate( ImmutableArray<PropertyBlock<T>> blocks )
	{
		if ( blocks.IsDefaultOrEmpty ) throw new ArgumentException( "Expected at least 2 blocks.", nameof(Blocks) );

		var prevTime = blocks[0].TimeRange.End;

		foreach ( var block in blocks.Skip( 1 ) )
		{
			if ( block.TimeRange.Start != prevTime )
			{
				throw new ArgumentException( "Expected blocks to be contiguous and ordered.", nameof(Blocks) );
			}

			prevTime = block.TimeRange.End;
		}

		return true;
	}
}
