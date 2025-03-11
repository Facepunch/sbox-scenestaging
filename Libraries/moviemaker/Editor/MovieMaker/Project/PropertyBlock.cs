using System.Collections.Immutable;
using System.Linq;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;
using static Sandbox.Game;

namespace Editor.MovieMaker;

#nullable enable

public interface IPropertyBlock
{
	MovieTimeRange TimeRange { get; }
	Type PropertyType { get; }

	object? GetValue( MovieTime time );
}

public interface IDynamicBlock
{
	event Action? Changed;
}

public interface IPropertyBlock<out T> : IPropertyBlock
{
	new T GetValue( MovieTime time );

	object? IPropertyBlock.GetValue( MovieTime time ) => GetValue( time );
	Type IPropertyBlock.PropertyType => typeof(T);
}

public abstract record PropertyBlock( MovieTimeRange TimeRange, Type PropertyType ) : IPropertyBlock
{
	public object? GetValue( MovieTime time ) => OnGetValue( time );

	protected abstract object? OnGetValue( MovieTime time );

	public PropertyBlock Slice( MovieTimeRange timeRange ) => OnSlice( timeRange ).Reduce();
	protected abstract PropertyBlock OnSlice( MovieTimeRange timeRange );

	public PropertyBlock Shift( MovieTime offset ) => OnShift( offset ).Reduce();
	protected abstract PropertyBlock OnShift( MovieTime offset );

	public PropertyBlock Stitch( PropertyBlock next ) => OnStitch( next ).Reduce();
	protected abstract PropertyBlock OnStitch( PropertyBlock next );

	public PropertyBlock Reduce()
	{
		var block = this;

		while ( block.Reduce() is { } reduced && reduced != block )
		{
			block = reduced;
		}

		return block;
	}

	protected abstract PropertyBlock OnReduce();

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

public abstract record PropertyBlock<T>( MovieTimeRange TimeRange ) : PropertyBlock( TimeRange, typeof(T) ), IPropertyBlock<T>
{
	public new abstract T GetValue( MovieTime time );

	public new PropertyBlock<T> Slice( MovieTimeRange timeRange )
	{
		return timeRange == TimeRange ? this : new PropertyBlockSlice<T>( this, TimeRange, 0d );
	}

	public new PropertyBlock<T> Shift( MovieTime offset )
	{
		return offset == default ? this : new PropertyBlockSlice<T>( this, TimeRange + offset, offset );
	}

	public PropertyBlock<T> Stitch( PropertyBlock<T> next ) => new PropertyBlockStitch<T>( [this, next] );

	public new virtual PropertyBlock<T> Reduce() => this;

	protected sealed override object? OnGetValue( MovieTime time ) => GetValue( time );
	protected sealed override PropertyBlock OnSlice( MovieTimeRange timeRange ) => Slice( timeRange );
	protected sealed override PropertyBlock OnShift( MovieTime offset ) => Shift( offset );
	protected sealed override PropertyBlock OnStitch( PropertyBlock next ) => Stitch( (PropertyBlock<T>)next );
	protected sealed override PropertyBlock OnReduce() => Reduce();
}

public sealed record SourceClipPropertyBlock<T>( ProjectSourceClip Source, CompiledPropertyTrack<T> Track, CompiledPropertyBlock<T> Block )
	: PropertyBlock<T>( Block.TimeRange )
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time );
}

public sealed record ConstantPropertyBlock<T>( MovieTimeRange TimeRange, T Value ) : PropertyBlock<T>( TimeRange )
{
	public override T GetValue( MovieTime time ) => Value;
}

public sealed record PropertyBlockSlice<T>( PropertyBlock<T> Block, MovieTimeRange TimeRange, MovieTime Offset = default )
	: PropertyBlock<T>( TimeRange )
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time.Clamp( TimeRange ) - Offset );

	public static bool CanMerge( PropertyBlockSlice<T> prev, PropertyBlockSlice<T> next )
	{
		if ( prev.Block != next.Block ) return false;
		if ( prev.Offset != next.Offset ) return false;

		return prev.TimeRange.End == next.TimeRange.Start;
	}

	public override PropertyBlock<T> Reduce()
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

		return this;
	}
}

public sealed record PropertyBlockStitch<T>( ImmutableArray<PropertyBlock<T>> Blocks )
	: PropertyBlock<T>( (Blocks[0].TimeRange.Start, Blocks[^1].TimeRange.End) )
{
	private readonly bool _validated = Validate( Blocks );

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

	private bool CanReduce()
	{
		if ( Blocks.Length == 1 ) return true;

		PropertyBlock<T>? prevBlock = null;

		foreach ( var block in Blocks )
		{
			// Can flatten nested stitched blocks

			if ( block is PropertyBlockStitch<T> ) return true;

			// Can merge adjacent slices that sliced the same block

			if ( prevBlock is PropertyBlockSlice<T> prevSlice
				&& block is PropertyBlockSlice<T> nextSlice
				&& PropertyBlockSlice<T>.CanMerge( prevSlice, nextSlice ) )
			{
				return true;
			}

			prevBlock = block;
		}

		return false;
	}

	public override PropertyBlock<T> Reduce()
	{
		if ( !CanReduce() ) return this;

		// We don't need to stitch a single block

		if ( Blocks.Length == 1 )
		{
			return Blocks[0];
		}

		// Can flatten nested stitched blocks

		var reduced = Blocks
			.SelectMany( x => x is PropertyBlockStitch<T> inner ? inner.Blocks : [x] )
			.ToList();

		for ( var i = reduced.Count - 2; i >= 0; i-- )
		{
			var prev = reduced[i];
			var next = reduced[i + 1];

			// Can merge adjacent slices that sliced the same block

			if ( prev is PropertyBlockSlice<T> prevSlice
				&& next is PropertyBlockSlice<T> nextSlice
				&& PropertyBlockSlice<T>.CanMerge( prevSlice, nextSlice ) )
			{
				reduced[i] = prevSlice with { TimeRange = prevSlice.TimeRange.Union( nextSlice.TimeRange ) };
				reduced.RemoveAt( i + 1 );
			}
		}

		return this;
	}
}

public sealed record PropertyBlockBlend<T>(
	PropertyBlock<T> Original,
	PropertyBlock<T> Overlay,
	TimeSelection Envelope )
	: PropertyBlock<T>( Original.TimeRange )
{
	private readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();

	public override T GetValue( MovieTime time )
	{
		var blend = Envelope.GetFadeValue( time );

		if ( blend >= 1f )
		{
			return Overlay.GetValue( time );
		}

		if ( blend <= 0f || _interpolator is not { } interpolator )
		{
			return Original.GetValue( time );
		}

		var a = Original.GetValue( time );
		var b = Overlay.GetValue( time );

		return interpolator.Interpolate( a, b, blend );
	}
}
