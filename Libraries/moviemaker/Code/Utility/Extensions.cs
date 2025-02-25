using System;

namespace Sandbox.MovieMaker;

#nullable enable

public static class Extensions
{
	public static IReadOnlyList<T> Slice<T>( this IReadOnlyList<T> list, int offset, int count )
	{
		if ( offset < 0 )
		{
			throw new ArgumentException( "Offset must be >= 0.", nameof( offset ) );
		}

		if ( list.Count < offset + count )
		{
			throw new ArgumentException( "Slice exceeds list element count.", nameof( count ) );
		}

		// Fast paths

		if ( count == 0 ) return Array.Empty<T>();
		if ( offset == 0 && count == list.Count ) return list;

		switch ( list )
		{
			case T[] array:
				return new ArraySegment<T>( array, offset, count );
			case ArraySegment<T> segment:
				return segment.Slice( offset, count );
		}

		// Slow copy

		return list.Skip( offset ).Take( count ).ToArray();
	}

	public static bool TryGetValue( this MovieTrack track, MovieTime time, out object? value )
	{
		value = null;

		if ( track.GetBlock( time ) is not { } block ) return false;
		if ( block.Data is not IValueData valueData ) return false;

		value = valueData.GetValue( time - block.TimeRange.Start );

		return true;
	}

	public static bool TryGetValue<T>( this MovieTrack track, MovieTime time, out T value )
	{
		value = default!;

		if ( track.GetBlock( time ) is not { } block ) return false;
		if ( block.Data is not IValueData<T> valueData ) return false;

		value = valueData.GetValue( time - block.TimeRange.Start );

		return true;
	}

	/// <summary>
	/// Looks for a block that contains the given <paramref name="time"/>.
	/// </summary>
	public static MovieBlock? GetBlock( this MovieTrack track, MovieTime time )
	{
		var blocks = track.Blocks;

		if ( blocks.Length == 0 ) return null;

		if ( blocks[0].TimeRange.Start > time ) return null;
		if ( blocks[^1].TimeRange.End < time ) return null;

		// TODO: binary search?

		// We go backwards because if we're exactly on a block boundary, we want to use the later block

		for ( var i = blocks.Length - 1; i >= 0; --i )
		{
			var block = blocks[i];

			if ( block.TimeRange.Contains( time ) )
			{
				return block;
			}
		}

		return null;
	}

	/// <summary>
	/// Enumerate blocks that intersect <paramref name="timeRange"/>.
	/// </summary>
	public static IEnumerable<MovieBlock> GetBlocks( this MovieTrack track, MovieTimeRange timeRange )
	{
		foreach ( var block in track.Blocks )
		{
			if ( block.TimeRange.Intersect( timeRange ) is not null )
			{
				yield return block;
			}
		}
	}

}
