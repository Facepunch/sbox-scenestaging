using Sandbox.MovieMaker;
using System;
using System.Linq;
using static Sandbox.PhysicsGroupDescription.BodyPart;
using static System.Net.Mime.MediaTypeNames;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Helper methods for working with <see cref="MovieProject"/>, <see cref="ProjectTrack"/>, or <see cref="ProjectBlock"/>.
/// </summary>
public static class ProjectExtensions
{
	public static IEnumerable<IProjectPropertyBlock> GetBlocks( this ProjectPropertyTrack track, MovieTimeRange timeRange )
	{
		return track.Blocks.Where( x => x.TimeRange.Intersect( timeRange ) is not null );
	}

	public static IEnumerable<PropertyBlock<T>> GetBlocks<T>( this ProjectPropertyTrack<T> track, MovieTimeRange timeRange )
	{
		return track.Blocks.Where( x => x.TimeRange.Intersect( timeRange ) is not null );
	}

	public static T GetValue<T>( this IReadOnlyList<IPropertyBlock<T>> blocks, MovieTime time )
	{
		return blocks.GetLastBlock( time ).GetValue( time );
	}

	public static T GetLastBlock<T>( this IReadOnlyList<T> blocks, MovieTime time )
		where T : IPropertyBlock
	{
		if ( blocks.Count == 0 ) throw new ArgumentException( "Expected at least one block.", nameof( blocks ) );

		if ( time <= blocks[0].TimeRange.Start ) return blocks[0];
		if ( time >= blocks[^1].TimeRange.End ) return blocks[^1];

		// TODO: binary search?

		// We go backwards because if we're exactly on a block boundary, we want to use the later block

		for ( var i = blocks.Count - 1; i >= 0; --i )
		{
			var block = blocks[i];

			if ( block.TimeRange.Start > time ) continue;

			return block;
		}

		return blocks[0];

	}

	public static T? GetBlock<T>( this IReadOnlyList<T> blocks, MovieTime time )
		where T : IPropertyBlock
	{
		if ( blocks.Count == 0 ) return default;

		var block = blocks.GetLastBlock( time );

		return block.TimeRange.Contains( time ) ? block : default;
	}

	public static PropertyBlock<T> Join<T>( this IEnumerable<PropertyBlock<T>> blocks )
	{
		return blocks.Aggregate( (PropertyBlock<T>?)null, ( s, x ) => s?.Join( x ) ?? x )
			?? throw new ArgumentException( "Expected at least one block.", nameof(blocks) );
	}

	public static PropertyBlock<T> Clamp<T>( this PropertyBlock<T> block, MovieTimeRange timeRange )
	{
		return block.Slice( block.TimeRange.Clamp( timeRange ) );
	}

	public static PropertyBlock<T> ClampStart<T>( this PropertyBlock<T> block, MovieTime start )
	{
		return block.Slice( block.TimeRange.ClampStart( start ) );
	}

	public static PropertyBlock<T> ClampEnd<T>( this PropertyBlock<T> block, MovieTime end )
	{
		return block.Slice( block.TimeRange.ClampEnd( end ) );
	}

	public static IReadOnlyList<PropertyBlock<T>> Cut<T>( this PropertyBlock<T> block, MovieTime time )
	{
		return block.Cut( [time] );
	}

	public static IReadOnlyList<PropertyBlock<T>> Cut<T>( this PropertyBlock<T> block, MovieTimeRange timeRange )
	{
		return block.Cut( timeRange.Start, timeRange.End );
	}

	public static IReadOnlyList<PropertyBlock<T>> Cut<T>( this PropertyBlock<T> block, params IEnumerable<MovieTime> times )
	{
		var list = new List<PropertyBlock<T>>();

		if ( block.TrySplit() is { } parts )
		{
			list.AddRange( parts );
		}
		else
		{
			list.Add( block );
		}

		foreach ( var time in times )
		{
			if ( list.GetBlock( time ) is not { } cutBlock )
			{
				continue;
			}

			if ( cutBlock.TimeRange.Start == time || cutBlock.TimeRange.End == time )
			{
				continue;
			}

			var index = list.IndexOf( cutBlock );

			list[index] = cutBlock.ClampEnd( time );
			list.Insert( index + 1, cutBlock.ClampStart( time ) );
		}

		return list;
	}

	/// <summary>
	/// For each pair of intersecting blocks from <paramref name="left"/> and <paramref name="right"/>, cut into individual
	/// blocks that exactly overlap and return them. Assumes the input block lists are ordered, and returns an ordered block list.
	/// </summary>
	public static IEnumerable<(PropertyBlock<T> Left, PropertyBlock<T> Right)> Zip<T>( this IReadOnlyList<PropertyBlock<T>> left, IReadOnlyList<PropertyBlock<T>> right )
	{
		if ( left.Count == 0 ) throw new ArgumentException( "Expected at least one block.", nameof(left) );
		if ( right.Count == 0 ) throw new ArgumentException( "Expected at least one block.", nameof(right) );

		var leftTimeRange = (left[0].TimeRange.Start, left[^1].TimeRange.End);
		var rightTimeRange = (right[0].TimeRange.Start, right[^1].TimeRange.End);

		if ( leftTimeRange != rightTimeRange )
		{
			throw new ArgumentException( "Block list need exactly overlapping time ranges.", nameof(right) );
		}

		foreach ( var leftPart in left )
		{
			foreach ( var rightPart in right )
			{
				if ( leftPart.TimeRange.Intersect( rightPart.TimeRange ) is not { IsEmpty: false } intersection ) continue;

				yield return (leftPart.Slice( intersection ), rightPart.Slice( intersection ));
			}
		}
	}
}
