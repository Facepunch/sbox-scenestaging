
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public static class EditHelpers
{
	public static bool Replace( this MovieTrack track, MovieTimeRange timeRange, IMovieBlockData? newData )
	{
		var changed = false;

		for ( var i = track.Blocks.Count - 1; i >= 0; --i )
		{
			var block = track.Blocks[i];

			if ( block.TimeRange.Intersect( timeRange ) is not { IsEmpty: false } intersection ) continue;

			changed = true;

			var headRange = new MovieTimeRange( block.Start, intersection.Start );
			var tailRange = new MovieTimeRange( intersection.End, block.End );

			if ( !headRange.IsEmpty && block.Slice( headRange ) is { } head )
			{
				track.AddBlock( headRange, head );
			}

			if ( !tailRange.IsEmpty && block.Slice( tailRange ) is { } tail )
			{
				track.AddBlock( tailRange, tail );
			}

			block.Remove();
		}

		// TODO: can re-use one of the removed blocks

		if ( newData is not null )
		{
			track.AddBlock( timeRange, newData );
			return true;
		}

		return changed;
	}

	public static bool Delete( this MovieTrack track, MovieTimeRange timeRange, bool shift )
	{
		var changed = track.Replace( timeRange, null );

		if ( shift )
		{
			foreach ( var block in track.Blocks )
			{
				if ( block.Start >= timeRange.End )
				{
					block.TimeRange -= timeRange.Duration;
					changed = true;
				}
			}
		}

		return changed;
	}

	public static IMovieBlockData? Slice( this MovieBlock srcBlock, MovieTimeRange srcTimeRange )
	{
		return srcBlock.Data is IMovieBlockValueData valueData
			? valueData.Slice( srcTimeRange - srcBlock.Start )
			: null;
	}

	public static void Sample<T>( this MovieBlock srcBlock, Span<T> dstSamples, MovieTimeRange dstTimeRange, MovieTimeRange srcTimeRange, int sampleRate )
	{
		if ( srcTimeRange.IsEmpty ) return;
		if ( dstTimeRange.Intersect( srcTimeRange ) is not { IsEmpty: false } intersection ) return;
		if ( srcBlock.Data is not IMovieBlockValueData<T> valueData ) return;

		var dstStartIndex = (intersection.Start - dstTimeRange.Start).GetFrameCount( sampleRate );
		var dstEndIndex = (intersection.End - dstTimeRange.Start).GetFrameCount( sampleRate );

		valueData.Sample( dstSamples[dstStartIndex..dstEndIndex], intersection - srcTimeRange.Start, sampleRate );
	}
}
