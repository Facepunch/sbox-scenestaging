using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public record struct InsertOptions(
	IEnumerable<IMovieBlock> Blocks,
	bool StitchStart = false,
	bool StitchEnd = false );

public static class EditingExtensions
{
	public static bool Splice( this MovieProjectTrack track, MovieTimeRange timeRange, MovieTime newDuration, InsertOptions? insertOptions = null )
	{
		var changed = false;

		MovieProjectBlock? head = null;
		MovieProjectBlock? tail = null;

		for ( var i = track.Blocks.Count - 1; i >= 0; --i )
		{
			var block = track.Blocks[i];

			if ( block.TimeRange.Intersect( timeRange ) is not { } intersection ) continue;

			changed = true;

			var headRange = new MovieTimeRange( block.Start(), intersection.Start );
			var tailRange = new MovieTimeRange( intersection.End, block.End() );

			if ( !headRange.IsEmpty ) head = track.AddBlock( block.Slice( headRange ) );
			if ( !tailRange.IsEmpty ) tail = track.AddBlock( block.Slice( tailRange ) );

			block.Remove();
		}

		// Shift everything after the splice

		foreach ( var block in track.Blocks )
		{
			if ( block.Start() < timeRange.End ) continue;

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

			if ( options.StitchStart && head is not null && block.Start() == timeRange.Start )
			{
				body = track.Stitch( head, body ) ?? body;
			}

			if ( options.StitchEnd && tail is not null && block.End() == timeRange.End )
			{
				body = track.Stitch( body, tail ) ?? body;
			}
		}

		return changed;
	}

	public static bool Delete( this MovieProjectTrack track, MovieTimeRange timeRange, bool shift )
	{
		return track.Splice( timeRange, shift ? MovieTime.Zero : timeRange.Duration );
	}

	public static bool Delete( this Session session, MovieTimeRange timeRange, bool shift )
	{
		var changed = false;

		foreach ( var track in session.EditableTracks )
		{
			if ( track.Delete( timeRange, shift ) )
			{
				changed = true;
				session.TrackModified( track );
			}
		}

		return changed;
	}

	public static bool Insert( this MovieProjectTrack track, MovieTimeRange timeRange )
	{
		return track.Splice( timeRange.Start, timeRange.Duration );
	}

	public static bool Insert( this Session session, MovieTimeRange timeRange )
	{
		var changed = false;

		foreach ( var track in session.EditableTracks )
		{
			if ( track.Insert( timeRange ) )
			{
				changed = true;
				session.TrackModified( track );
			}
		}

		return changed;
	}

	public static MovieProjectBlock? Stitch( this MovieProjectTrack track, MovieProjectBlock? left, MovieProjectBlock? right )
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

		if ( left.End() != right.Start() ) return null;

		var sampleRate = track.Project.SampleRate;
		var timeRange = left.TimeRange.Union( right.TimeRange );
		var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );
		var samples = Array.CreateInstance( track.TargetType, sampleCount );

		left.Sample( samples, left.TimeRange, left.TimeRange - timeRange.Start, sampleRate );
		right.Sample( samples, right.TimeRange, right.TimeRange - timeRange.Start, sampleRate );

		left.Remove();
		right.Remove();

		return track.AddBlock( timeRange, track.TargetType.CreateSamplesData( sampleRate, samples ) );
	}
}

[Flags]
public enum SnapFlag
{
	None = 0,

	// General
	Frame = 1,
	MinorTick = 2,
	MajorTick = 4,
	PlayHead = 8,
	TrackBlock = 0x10,
	PasteBlock = 0x20,

	// Keyframe edit mode
	Keyframe = 0x1_0000,

	// Motion edit mode
	SelectionTotalStart = 0x1_0000,
	SelectionPeakStart = 0x2_0000,
	SelectionPeakEnd = 0x4_0000,
	SelectionTotalEnd = 0x8_0000,

	SelectionStart = SelectionTotalStart | SelectionPeakStart,
	SelectionEnd = SelectionPeakEnd | SelectionTotalEnd,
	Selection = SelectionStart | SelectionEnd
}

public struct TimeSnapHelper
{
	public MovieTime Time { get; }

	public MovieTime MaxSnap { get; set; }
	public SnapFlag Ignore { get; set; }

	public MovieTime BestTime { get; private set; }
	public float BestScore { get; private set; } = float.MaxValue;

	public TimeSnapHelper( MovieTime time, MovieTime maxSnap, SnapFlag ignore )
	{
		Time = BestTime = time;
		BestScore = float.PositiveInfinity;

		MaxSnap = maxSnap;
		Ignore = ignore;
	}

	public void Add( SnapFlag flag, MovieTime time, int priority = 0, bool force = false )
	{
		if ( (Ignore & flag) != 0 ) return;

		var timeDiff = (time - Time).Absolute;

		if ( !force && timeDiff * Math.Max( 4 - priority, 1 ) > MaxSnap * 4 ) return;

		var score = (float)(timeDiff.TotalSeconds / MaxSnap.TotalSeconds) - priority;

		if ( score >= BestScore ) return;

		BestScore = score;
		BestTime = time;
	}

	public void Add( TimeSnapHelper helper )
	{
		if ( helper.BestScore >= BestScore ) return;

		BestScore = helper.BestScore;
		BestTime = helper.BestTime - (helper.Time - Time);
	}
}
