namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Helper methods for working with <see cref="IClip"/>, <see cref="ITrack"/>, or <see cref="IBlock"/>.
/// </summary>
public static class TrackExtensions
{
	/// <summary>
	/// How deeply are we nested? Root tracks have depth <c>0</c>.
	/// </summary>
	public static int GetDepth( this ITrack track ) => track.Parent is null ? 0 : track.Parent.GetDepth() + 1;

	public static IEnumerable<IBlock> GetBlocks( this IBlockTrack track, MovieTimeRange timeRange )
	{
		return track.Blocks.Where( x => x.TimeRange.Intersect( timeRange ) is not null );
	}

	public static IEnumerable<T> GetBlocks<T>( this IBlockTrack<T> track, MovieTimeRange timeRange )
		where T : IBlock
	{
		return track.Blocks.Where( x => x.TimeRange.Intersect( timeRange ) is not null );
	}

	public static T? GetBlock<T>( this IBlockTrack<T> track, MovieTime time )
		where T : IBlock
	{
		if ( !track.TimeRange.Contains( time ) ) return default;

		// TODO: binary search?

		// We go backwards because if we're exactly on a block boundary, we want to use the later block

		var blocks = track.Blocks;

		for ( var i = blocks.Count - 1; i >= 0; --i )
		{
			var block = blocks[i];

			if ( block.TimeRange.Start > time ) continue;
			if ( block.TimeRange.End < time ) break;

			return block;
		}

		return default;
	}
}
