using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Represents a duration of time in a movie. Uses fixed point so precision is consistent at any absolute time.
/// Defaults to <see cref="Zero"/>.
/// </summary>
[JsonConverter( typeof( MovieTimeConverter ) )]
public readonly struct MovieTime : IEquatable<MovieTime>, IComparable<MovieTime>
{
	/// <summary>
	/// How many <see cref="Ticks"/> per second. This value should nicely divide into
	/// common frame rates.
	/// </summary>
	public const int TickRate = 3600;

	public static MovieTime Zero => default;
	public static MovieTime Epsilon => FromTicks( 1 );
	public static MovieTime OneSecond { get; } = FromSeconds( 1d );
	public static MovieTime MinValue => FromTicks( int.MinValue );
	public static MovieTime MaxValue => FromTicks( int.MaxValue );

	/// <summary>
	/// Frame rates <c>&lt;= 120</c> that can be perfectly represented by <see cref="TickRate"/>, in ascending order.
	/// Venturing outside these rates will lead to some frames being slightly different durations than others.
	/// </summary>
	public static IReadOnlyList<int> SupportedFrameRates { get; } = Enumerable.Range( 1, 120 )
		.Where( x => TickRate % x == 0 )
		.ToImmutableArray();

	public static MovieTime FromTicks( int ticks ) => new ( ticks );

	public static MovieTime FromSeconds( double time )
	{
		return FromTicks( (int)Math.Round( time * TickRate ) );
	}

	public static MovieTime FromFrames( int frameCount, int frameRate )
	{
		return FromTicks( (int)((long)frameCount * TickRate / frameRate) );
	}

	public static MovieTime Max( MovieTime a, MovieTime b )
	{
		return FromTicks( Math.Max( a._ticks, b._ticks ) );
	}

	public static MovieTime Min( MovieTime a, MovieTime b )
	{
		return FromTicks( Math.Min( a._ticks, b._ticks ) );
	}

	public static MovieTime Distance( MovieTime a, MovieTime b )
	{
		return (a - b).Absolute;
	}

	public static MovieTime Lerp( MovieTime a, MovieTime b, double fraction )
	{
		return fraction <= 0d ? a : fraction >= 1d ? b : FromTicks( (int)(a.Ticks + (b.Ticks - a.Ticks) * fraction) );
	}

	private readonly int _ticks;

	public int Ticks => _ticks;

	public bool IsZero => _ticks == 0;
	public bool IsPositive => _ticks > 0;
	public bool IsNegative => _ticks < 0;

	public double TotalSeconds => (double)_ticks / TickRate;

	public MovieTime Absolute => _ticks < 0 ? -this : this;

	private MovieTime( int ticks )
	{
		_ticks = ticks;
	}

	public MovieTime Clamp( MovieTimeRange range )
	{
		return Max( range.Start, Min( range.End, this ) );
	}

	public MovieTime SnapToGrid( MovieTime gridInterval )
	{
		if ( gridInterval.Ticks <= 0 ) return this;

		return FromTicks( (Ticks + gridInterval.Ticks / 2) / gridInterval.Ticks * gridInterval.Ticks );
	}

	/// <summary>
	/// Given a <paramref name="frameRate"/>, how many frames have passed before reaching
	/// this time.
	/// </summary>
	public int GetFrameIndex( int frameRate )
	{
		return (int)((long)_ticks * frameRate / TickRate);
	}

	/// <summary>
	/// Given a <paramref name="frameRate"/>, how many frames have passed before reaching
	/// this time, and how far into the current frame are we.
	/// </summary>
	public int GetFrameIndex( int frameRate, out MovieTime remainder )
	{
		var frameCount = GetFrameIndex( frameRate );

		remainder = this - FromFrames( frameCount, frameRate );

		return frameCount;
	}

	/// <summary>
	/// Given a <paramref name="frameRate"/>, how many frames would need to be allocated
	/// to represent every moment of time up until now. This is always at least <c>1</c>,
	/// and will be <c>1</c> more than <see cref="GetFrameIndex(int)"/> unless this time
	/// is exactly on a frame boundary.
	/// </summary>
	public int GetFrameCount( int frameRate )
	{
		return Math.Max( 1, (int)(((long)_ticks * frameRate + TickRate - 1) / TickRate) );
	}

	public bool Equals( MovieTime other ) => _ticks == other._ticks;
	public int CompareTo( MovieTime other ) => _ticks.CompareTo( other._ticks );

	public override bool Equals( [NotNullWhen( true )] object? obj ) => obj is MovieTime span && Equals( span );
	public override int GetHashCode() => _ticks.GetHashCode();
	public override string ToString() => TimeSpan.FromSeconds( TotalSeconds ).ToString( @"mm\:ss\.fff" );

	#region Operators

	public static MovieTime operator +( MovieTime time ) => time;
	public static MovieTime operator -( MovieTime time ) => FromTicks( -time._ticks );
	public static MovieTime operator +( MovieTime a, MovieTime b ) => FromTicks( a._ticks + b._ticks );
	public static MovieTime operator -( MovieTime a, MovieTime b ) => FromTicks( a._ticks - b._ticks );
	public static MovieTime operator *( MovieTime time, int scale ) => FromTicks( time._ticks * scale );
	public static MovieTime operator *( int scale, MovieTime time ) => FromTicks( time._ticks * scale );

	public static bool operator ==( MovieTime a, MovieTime b ) => a._ticks == b._ticks;
	public static bool operator !=( MovieTime a, MovieTime b ) => a._ticks != b._ticks;

	public static bool operator >( MovieTime a, MovieTime b ) => a._ticks > b._ticks;
	public static bool operator <( MovieTime a, MovieTime b ) => a._ticks < b._ticks;
	public static bool operator >=( MovieTime a, MovieTime b ) => a._ticks >= b._ticks;
	public static bool operator <=( MovieTime a, MovieTime b ) => a._ticks <= b._ticks;

	#endregion
}

/// <summary>
/// Represents a segment of time, given by <see cref="Start"/> and <see cref="End"/> times.
/// </summary>
/// <param name="Start">Minimum time in the range.</param>
/// <param name="End">Maximum time in the range.</param>
public readonly record struct MovieTimeRange( MovieTime Start, MovieTime End )
{
	[JsonIgnore]
	public MovieTime Duration => End - Start;

	[JsonIgnore]
	public MovieTime Center => MovieTime.Lerp( Start, End, 0.5 );

	[JsonIgnore]
	public bool IsEmpty => Start >= End;

	public MovieTimeRange? Intersect( MovieTimeRange other )
	{
		if ( Start > other.End || End < other.Start ) return null;

		return new MovieTimeRange( MovieTime.Max( Start, other.Start ), MovieTime.Min( End, other.End ) );
	}

	public MovieTimeRange Union( MovieTimeRange other )
	{
		return new MovieTimeRange( MovieTime.Min( Start, other.Start ), MovieTime.Max( End, other.End ) );
	}

	public MovieTimeRange Clamp( MovieTimeRange range )
	{
		return new MovieTimeRange( Start.Clamp( range ), End.Clamp( range ) );
	}

	public MovieTimeRange Grow( MovieTime startEndDelta ) => Grow( startEndDelta, startEndDelta );

	public MovieTimeRange Grow( MovieTime startDelta, MovieTime endDelta )
	{
		if ( startDelta.IsZero && endDelta.IsZero ) return this;

		var start = Start - startDelta;
		var end = End + endDelta;

		if ( end >= start )
		{
			return new MovieTimeRange( start, end );
		}

		// Work out how far start / end can move before they cross

		var t = (Start - End).Ticks / (double)(startDelta + endDelta).Ticks;

		return MovieTime.Lerp( start, start + startDelta, t );
	}

	public bool Contains( MovieTime time )
	{
		return time >= Start && time < End;
	}

	public bool Contains( MovieTimeRange timeRange )
	{
		return timeRange.Start >= Start && timeRange.End <= End;
	}

	public float GetFraction( MovieTime time )
	{
		return time <= Start ? 0f : time >= End ? 1f : (float)(time - Start).Ticks / (End - Start).Ticks;
	}

	public override string ToString() => $"[{Start}, {End}]";

	#region Operators

	public static implicit operator MovieTimeRange( MovieTime time ) => new( time, time );

	public static implicit operator MovieTimeRange( (MovieTime, MovieTime) tuple ) => new( tuple.Item1, tuple.Item2 );

	public static MovieTimeRange operator +( MovieTimeRange range, MovieTime offset ) => (range.Start + offset, range.End + offset);
	public static MovieTimeRange operator -( MovieTimeRange range, MovieTime offset ) => (range.Start - offset, range.End - offset);

	#endregion
}

file class MovieTimeConverter : JsonConverter<MovieTime>
{
	public override MovieTime Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
	{
		return MovieTime.FromTicks( reader.GetInt32() );
	}

	public override void Write( Utf8JsonWriter writer, MovieTime value, JsonSerializerOptions options )
	{
		writer.WriteNumberValue( value.Ticks );
	}
}
