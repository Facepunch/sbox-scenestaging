using Sandbox.MovieMaker;
using System.Linq;

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
		Log.Info( $"Join! {string.Join( ", ", blocks.Select( x => x.TimeRange ) )}" );
		return blocks.Aggregate( (PropertyBlock<T>?)null, ( s, x ) =>
		       {
			       Log.Info( $"{s?.TimeRange}, {x.TimeRange}" );
			       return s?.Join( x ) ?? x;
		       } )
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
}
