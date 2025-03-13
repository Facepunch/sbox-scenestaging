using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDerivedType( typeof(PropertyBlockJoin<>), "Join" )]
partial record PropertyBlock<T>
{
	public PropertyBlock<T> Join( PropertyBlock<T> next, bool smooth )
	{
		if ( next.TimeRange.Start < TimeRange.End )
		{
			throw new ArgumentException( "Can't join overlapping blocks.", nameof(next) );
		}

		var prev = this;

		if ( prev.TimeRange.End < next.TimeRange.Start )
		{
			prev = prev.Slice( prev.TimeRange with { End = next.TimeRange.Start } );
		}

		return new PropertyBlockJoin<T>( [prev, next], smooth ).Reduce();
	}

	IProjectPropertyBlock IProjectPropertyBlock.Join( IProjectPropertyBlock next, bool smooth ) =>
		Join( (PropertyBlock<T>)next, smooth );
}

file sealed record PropertyBlockJoin<T>( ImmutableArray<PropertyBlock<T>> Blocks, bool Smooth )
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
		if ( blocks.IsDefaultOrEmpty ) throw new ArgumentException( "Expected at least 2 blocks.", nameof( Blocks ) );

		var prevTime = blocks[0].TimeRange.End;

		foreach ( var block in blocks.Skip( 1 ) )
		{
			if ( block.TimeRange.Start != prevTime )
			{
				throw new ArgumentException( "Expected blocks to be contiguous and ordered.", nameof( Blocks ) );
			}

			prevTime = block.TimeRange.End;
		}

		return true;
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return Blocks
			.SelectMany( x => x.TimeRange.Intersect( timeRange ) is { } intersection
				? x.GetPaintHintTimes( intersection.Grow( 0, -MovieTime.Epsilon ) )
				: [] );
	}

	private bool CanReduce()
	{
		if ( Blocks.Length == 1 ) return true;

		PropertyBlock<T>? prevBlock = null;

		foreach ( var nextBlock in Blocks )
		{
			// Can remove empty blocks

			if ( nextBlock.TimeRange.IsEmpty ) return true;

			// Can flatten nested joined blocks

			if ( nextBlock is PropertyBlockJoin<T> inner && inner.Smooth == Smooth ) return true;

			// Check if neighbors can be merged

			if ( prevBlock?.TryMerge( nextBlock ) is not null )
			{
				return true;
			}

			prevBlock = nextBlock;
		}

		return false;
	}

	protected override PropertyBlock<T> OnReduce()
	{
		if ( !CanReduce() ) return this;

		// We don't need to join a single block

		if ( Blocks.Length == 1 )
		{
			return Blocks[0];
		}

		// Can flatten nested joined blocks, and remove inner blocks with zero length

		var firstIndex = 0;
		var lastIndex = Blocks.Length;

		var reduced = Blocks
			.Where( ( x, i ) => !x.TimeRange.IsEmpty && i != firstIndex && i != lastIndex )
			.SelectMany( x => x is PropertyBlockJoin<T> inner && inner.Smooth == Smooth ? inner.Blocks : [x] )
			.ToList();

		for ( var i = reduced.Count - 2; i >= 0; i-- )
		{
			var prev = reduced[i];
			var next = reduced[i + 1];

			// Try to merge neighboring blocks

			if ( prev.TryMerge( next ) is { } merged )
			{
				reduced[i] = merged;
				reduced.RemoveAt( i + 1 );
			}
		}

		return this;
	}

	public override string ToString()
	{
		return $"Join {{ Smooth = {Smooth}, Blocks = [{string.Join( ", ", Blocks )}] }}";
	}
}
