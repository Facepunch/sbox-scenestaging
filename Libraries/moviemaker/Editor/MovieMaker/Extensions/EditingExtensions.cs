using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

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

public readonly record struct SnapOptions(
	SnapFlag IgnoreFlags = SnapFlag.None,
	ITrackView? IgnoreTrack = null,
	ITrackBlock? IgnoreBlock = null,
	params MovieTime[] SnapOffsets )
{
	public static implicit operator SnapOptions( SnapFlag flags ) => new( flags );
}

public struct TimeSnapHelper
{
	public MovieTime Time { get; }

	public MovieTime MaxSnap { get; set; }
	public SnapOptions Options { get; }

	public MovieTime BestTime { get; private set; }
	public float BestScore { get; private set; } = float.MaxValue;

	public TimeSnapHelper( MovieTime time, MovieTime maxSnap, SnapOptions options )
	{
		Time = BestTime = time;
		BestScore = float.PositiveInfinity;

		MaxSnap = maxSnap;
		Options = options;
	}

	public void Add( SnapFlag flag, MovieTime time, int priority = 0, bool force = false )
	{
		if ( (Options.IgnoreFlags & flag) != 0 ) return;

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
