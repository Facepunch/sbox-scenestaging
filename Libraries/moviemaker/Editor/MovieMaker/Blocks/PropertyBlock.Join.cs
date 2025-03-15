using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial interface IProjectPropertyBlock
{
	IProjectPropertyBlock Join( IProjectPropertyBlock next );
	IReadOnlyList<IProjectPropertyBlock>? TrySplit();
}

partial class PropertyBlock<T>
{
	[return: NotNullIfNotNull( nameof(lhs) ), NotNullIfNotNull( nameof(rhs) )]
	public static PropertyBlock<T>? operator +( PropertyBlock<T>? lhs, PropertyBlock<T>? rhs )
	{
		return lhs is null ? rhs : lhs.Join( rhs );
	}

	public PropertyBlock<T> Join( PropertyBlock<T>? next )
	{
		if ( next is null ) return this;

		if ( next.TimeRange.Start < TimeRange.End )
		{
			throw new ArgumentException( $"Can't join overlapping blocks ({TimeRange}, {next.TimeRange}).", nameof(next) );
		}

		var prev = this;

		if ( prev.TimeRange.End < next.TimeRange.Start )
		{
			prev = prev.Slice( prev.TimeRange with { End = next.TimeRange.Start } );
		}

		var prevJoin = prev as PropertyBlockJoin<T>;
		var nextJoin = next as PropertyBlockJoin<T>;

		if ( prevJoin is not null && nextJoin is not null )
		{
			return new PropertyBlockJoin<T>( [..prevJoin.Blocks, ..nextJoin.Blocks] ).Reduce();
		}

		if ( prevJoin is not null )
		{
			return new PropertyBlockJoin<T>( [..prevJoin.Blocks, next] ).Reduce();
		}

		if ( nextJoin is not null )
		{
			return new PropertyBlockJoin<T>( [prev, ..nextJoin.Blocks] ).Reduce();
		}

		return new PropertyBlockJoin<T>( [prev, next] ).Reduce();
	}

	public IReadOnlyList<PropertyBlock<T>>? TrySplit() => OnTrySplit();

	protected virtual IReadOnlyList<PropertyBlock<T>>? OnTrySplit() => null;

	IProjectPropertyBlock IProjectPropertyBlock.Join( IProjectPropertyBlock next ) =>
		Join( (PropertyBlock<T>)next );

	IReadOnlyList<IProjectPropertyBlock>? IProjectPropertyBlock.TrySplit() => TrySplit();
}

[JsonDiscriminator( "Join" )]
file sealed class PropertyBlockJoin<T> : PropertyBlock<T>
{
	public ImmutableArray<PropertyBlock<T>> Blocks { get; }

	public PropertyBlockJoin( ImmutableArray<PropertyBlock<T>> blocks )
		: base( (blocks[0].TimeRange.Start, blocks[^1].TimeRange.End) )
	{
		if ( blocks.Length < 2 )
		{
			throw new ArgumentException( "Expected at least 2 blocks.", nameof(blocks) );
		}

		var prevTime = blocks[0].TimeRange.End;

		foreach ( var block in blocks.Skip( 1 ) )
		{
			if ( block.TimeRange.Start != prevTime )
			{
				throw new ArgumentException( "Expected blocks to be contiguous and ordered.", nameof(blocks) );
			}

			prevTime = block.TimeRange.End;
		}

		foreach ( var block in blocks.Skip( 1 ).Take( blocks.Length - 2 ) )
		{
			if ( block.TimeRange.IsEmpty )
			{
				throw new ArgumentException( "Expected inner blocks to have non-zero duration.", nameof(blocks) );
			}
		}

		Blocks = blocks;
	}

	protected override T OnGetValue( MovieTime time )
	{
		for ( var i = Blocks.Length - 1; i >= 0; --i )
		{
			if ( Blocks[i].TimeRange.Contains( time ) ) return Blocks[i].GetValue( time );
		}

		throw new Exception( "Expected blocks to be connected." );
	}

	protected override IReadOnlyList<PropertyBlock<T>> OnTrySplit() => Blocks;

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return Blocks
			.Where( x => x.TimeRange.Intersect( timeRange ) is not null )
			.Select( x => x.Slice( x.TimeRange.Clamp( timeRange ) ) )
			.Join();
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return Blocks.Select( x => x.Shift( offset ) ).Join();
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

			if ( nextBlock is PropertyBlockJoin<T> ) return true;

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

		var lastIndex = Blocks.Length;

		var reduced = Blocks
			.Where( ( x, i ) => !x.TimeRange.IsEmpty && i != lastIndex )
			.SelectMany( x => x is PropertyBlockJoin<T> inner ? inner.Blocks : [x] )
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

		return new PropertyBlockJoin<T>( [..reduced] );
	}

	protected override int OnGetHashCode()
	{
		var hash = new HashCode();

		foreach ( var block in Blocks )
		{
			hash.Add( block );
		}

		return hash.ToHashCode();
	}

	protected override bool EqualsBlock( PropertyBlock<T> other )
	{
		return other is PropertyBlockJoin<T> join
			&& Blocks.SequenceEqual( join.Blocks );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return Blocks
			.SelectMany( x => x.TimeRange.Intersect( timeRange ) is { } intersection
				? x.GetPaintHintTimes( intersection.Grow( 0, -MovieTime.Epsilon ) )
				: [] );
	}
}
