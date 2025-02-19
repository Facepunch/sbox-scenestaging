using System.Linq;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public record struct InsertOptions(
	IEnumerable<IMovieBlock> Blocks,
	bool StitchStart = false,
	bool StitchEnd = false );

public static class EditHelpers
{
	public static bool Splice( this MovieTrack track, MovieTimeRange timeRange, MovieTime newDuration, InsertOptions? insertOptions = null )
	{
		var changed = false;

		MovieBlock? head = null;
		MovieBlock? tail = null;

		for ( var i = track.Blocks.Count - 1; i >= 0; --i )
		{
			var block = track.Blocks[i];

			if ( block.TimeRange.Intersect( timeRange ) is not { } intersection ) continue;

			changed = true;

			var headRange = new MovieTimeRange( block.Start, intersection.Start );
			var tailRange = new MovieTimeRange( intersection.End, block.End );

			if ( !headRange.IsEmpty ) head = track.AddBlock( block.Slice( headRange ) );
			if ( !tailRange.IsEmpty ) tail = track.AddBlock( block.Slice( tailRange ) );

			block.Remove();
		}

		// Shift everything after the splice

		foreach ( var block in track.Blocks )
		{
			if ( block.Start < timeRange.End ) continue;

			block.TimeRange += newDuration - timeRange.Duration;

			changed = true;
		}

		if ( insertOptions is not { } options ) return changed;

		timeRange = (timeRange.Start, timeRange.Start + newDuration);

		// Insert new blocks

		foreach ( var block in options.Blocks )
		{
			changed = true;

			var body = track.AddBlock( block );

			if ( options.StitchStart && head is not null && block.TimeRange.Start == timeRange.Start )
			{
				body = track.Stitch( head, body ) ?? body;
			}

			if ( options.StitchEnd && tail is not null && block.TimeRange.End == timeRange.End )
			{
				body = track.Stitch( body, tail ) ?? body;
			}
		}

		return changed;
	}

	public static bool Delete( this MovieTrack track, MovieTimeRange timeRange, bool shift )
	{
		return track.Splice( timeRange, shift ? timeRange.Duration : MovieTime.Zero );
	}

	public static bool Delete( this Session session, MovieTimeRange timeRange, bool shift )
	{
		if ( session.Clip is not { } clip ) return false;

		var changed = false;

		foreach ( var track in clip.AllTracks )
		{
			if ( !track.CanModify() ) continue;

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
			if ( !track.CanModify() ) continue;

			if ( track.Insert( timeRange ) )
			{
				changed = true;
				session.TrackModified( track );
			}
		}

		return changed;
	}

	public static MovieBlock? Stitch( this MovieTrack track, MovieBlock? left, MovieBlock? right )
	{
		if ( left is null || right is null ) return null;

		if ( left.Track != track )
		{
			throw new ArgumentException( "Block doesn't belong to this track.", nameof(left) );
		}

		if ( right.Track != track )
		{
			throw new ArgumentException( "Block doesn't belong to this track.", nameof(right) );
		}

		if ( left.End != right.Start ) return null;

		var sampleRate = track.Clip.DefaultSampleRate;
		var timeRange = left.TimeRange.Union( right.TimeRange );
		var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );
		var samples = Array.CreateInstance( track.PropertyType, sampleCount );

		left.Sample( samples, left.TimeRange, left.TimeRange - timeRange.Start, sampleRate );
		right.Sample( samples, right.TimeRange, right.TimeRange - timeRange.Start, sampleRate );

		left.Remove();
		right.Remove();

		return track.AddBlock( timeRange, CreateSamplesData( track.PropertyType, sampleRate, SampleInterpolationMode.Linear, samples ) );
	}

	public static IEnumerable<MovieBlockSlice> Slice( this MovieTrack track, MovieTimeRange timeRange ) =>
		track.GetCuts( timeRange ).Select( x => x.Block.Slice( x.TimeRange ) );

	public static MovieBlockSlice Slice( this MovieBlock srcBlock, MovieTimeRange timeRange ) =>
		new( timeRange, srcBlock.Data.Slice( timeRange - srcBlock.Start ) );

	public static IMovieBlockData Slice( this IMovieBlockData data, MovieTimeRange timeRange )
	{
		return data is not IMovieBlockValueData valueData ? data : valueData.Slice( timeRange );
	}

	public static void Sample( this IMovieBlock block, Array dstSamples, MovieTimeRange srcTimeRange, MovieTimeRange dstTimeRange, int sampleRate )
	{
		if ( block.Data is not IMovieBlockValueData valueData ) return;

		var dstStartIndex = dstTimeRange.Start.GetFrameIndex( sampleRate );
		var dstEndIndex = dstTimeRange.End.GetFrameCount( sampleRate );

		if ( dstStartIndex < 0 )
		{
			srcTimeRange = (srcTimeRange.Start + MovieTime.FromFrames( -dstStartIndex, sampleRate ), srcTimeRange.End);
			dstStartIndex = 0;
		}

		if ( dstEndIndex > dstSamples.Length )
		{
			srcTimeRange = (srcTimeRange.Start, srcTimeRange.End - MovieTime.FromFrames( dstEndIndex - dstSamples.Length, sampleRate ));
			dstEndIndex = dstSamples.Length;
		}

		if ( dstEndIndex <= dstStartIndex || srcTimeRange.IsEmpty ) return;

		valueData.Sample( dstSamples, dstStartIndex, dstEndIndex - dstStartIndex, srcTimeRange - block.TimeRange.Start, sampleRate );
	}

	public static IConstantData CreateConstantData( Type type, object? value )
	{
		return (IConstantData)Activator.CreateInstance( typeof(ConstantData<>).MakeGenericType( type ), value )!;
	}

	public static ISamplesData CreateSamplesData( Type type,
		int sampleRate,
		SampleInterpolationMode interpolation,
		Array samples,
		MovieTime firstSampleTime = default )
	{
		return (ISamplesData)Activator.CreateInstance( typeof(SamplesData<>).MakeGenericType( type ),
			sampleRate, interpolation, samples, firstSampleTime )!;
	}

	public static bool CanModify( this MovieTrack? track )
	{
		if ( track is not { IsValid: true } ) return false;

		while ( track is not null )
		{
			if ( track.ReadEditorData()?.Locked is true ) return false;

			track = track.Parent;
		}

		return true;
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
