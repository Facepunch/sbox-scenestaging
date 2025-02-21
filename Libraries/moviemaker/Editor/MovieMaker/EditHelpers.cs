
using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public static class EditHelpers
{
	public static bool Splice( this MovieTrack track, MovieTimeRange timeRange, MovieTime newDuration, IEnumerable<IMovieBlock>? newBlocks = null, MovieTime newBlockOffset = default )
	{
		var changed = false;

		for ( var i = track.Blocks.Count - 1; i >= 0; --i )
		{
			var block = track.Blocks[i];

			if ( block.TimeRange.Intersect( timeRange ) is not { IsEmpty: false } intersection ) continue;

			changed = true;

			var headRange = new MovieTimeRange( block.Start, intersection.Start );
			var tailRange = new MovieTimeRange( intersection.End, block.End );

			if ( !headRange.IsEmpty ) track.AddBlock( block.Slice( headRange ) );
			if ( !tailRange.IsEmpty ) track.AddBlock( block.Slice( tailRange ) );

			block.Remove();
		}

		if ( newBlocks is null ) return changed;

		// TODO: can re-use the removed blocks

		foreach ( var block in newBlocks )
		{
			if ( block.TimeRange.IsEmpty ) continue;

			track.AddBlock( block );
			changed = true;
		}

		return changed;
	}

	public static bool Delete( this MovieTrack track, MovieTimeRange timeRange, bool shift )
	{
		return track.Splice( timeRange, shift ? MovieTime.Zero : timeRange.Duration );
	}

	public static bool Delete( this Session session, MovieTimeRange timeRange, bool shift )
	{
		if ( session.Clip is not { } clip ) return false;

		var changed = false;

		foreach ( var track in clip.AllTracks )
		{
			if ( track.Delete( timeRange, shift ) )
			{
				changed = true;
				session.TrackModified( track );
			}
		}

		return changed;
	}

	public static bool Insert( this MovieTrack track, MovieTimeRange timeRange )
	{
		return track.Splice( timeRange.Start, timeRange.Duration );
	}

	public static bool Insert( this Session session, MovieTimeRange timeRange )
	{
		if ( session.Clip is not { } clip ) return false;

		var changed = false;

		foreach ( var track in clip.AllTracks )
		{
			if ( track.Insert( timeRange ) )
			{
				changed = true;
				session.TrackModified( track );
			}
		}

		return changed;
	}

	public static void Sample<T>( this MovieBlock srcBlock, Span<T> dstSamples, MovieTimeRange dstTimeRange, MovieTimeRange srcTimeRange, int sampleRate )
	{
		if ( srcTimeRange.IsEmpty ) return;
		if ( dstTimeRange.Intersect( srcTimeRange ) is not { IsEmpty: false } intersection ) return;
		if ( srcBlock.Data is not IMovieBlockValueData<T> valueData ) return;

		var dstStartIndex = (intersection.Start - dstTimeRange.Start).GetFrameCount( sampleRate );
		var dstEndIndex = (intersection.End - dstTimeRange.Start).GetFrameCount( sampleRate ) + 1;

		valueData.Sample( dstSamples[dstStartIndex..dstEndIndex], intersection - srcTimeRange.Start, sampleRate );
	}

	public static IEnumerable<MovieBlockSlice> Slice( this MovieTrack track, MovieTimeRange timeRange ) =>
		track.GetCuts( timeRange ).Select( x => x.Block.Slice( x.TimeRange ) );

	public static MovieBlockSlice Slice( this MovieBlock srcBlock, MovieTimeRange timeRange ) =>
		new( timeRange, srcBlock.Data.Slice( timeRange - srcBlock.Start ) );

	public static IMovieBlockData Slice( this IMovieBlockData data, MovieTimeRange timeRange )
	{
		return data is not IMovieBlockValueData valueData ? data : valueData.Slice( timeRange );
	}
}

public struct TimeSnapHelper
{
	private readonly MovieTime _defaultTime;

	public MovieTime MaxSnap { get; set; }

	public MovieTime BestTime { get; private set; }
	public float BestScore { get; private set; } = float.MaxValue;

	public TimeSnapHelper( MovieTime defaultTime, MovieTime maxSnap )
	{
		_defaultTime = BestTime = defaultTime;
		BestScore = float.PositiveInfinity;

		MaxSnap = maxSnap;
	}

	public void Add( MovieTime time, int priority = 0 )
	{
		var timeDiff = (time - _defaultTime).Absolute;

		if ( timeDiff > MaxSnap ) return;

		var score = (float)(timeDiff.TotalSeconds / MaxSnap.TotalSeconds) - priority;

		if ( score >= BestScore ) return;

		BestScore = score;
		BestTime = time;
	}

	public void Add( TimeSnapHelper helper )
	{
		if ( helper.BestScore >= BestScore ) return;

		BestScore = helper.BestScore;
		BestTime = helper.BestTime - (helper._defaultTime - _defaultTime);
	}
}
