using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Represents a duration of time in a movie.
/// </summary>
[JsonConverter( typeof( MovieTimeConverter ) )]
public readonly struct MovieTime : IEquatable<MovieTime>, IComparable<MovieTime>
{
	// Fixed point, so we don't need to use epsilons everywhere

	public const int TickRate = 3600;

	public static MovieTime Zero => default;
	public static MovieTime OneSecond { get; } = FromSeconds( 1d );

	public static IReadOnlyList<int> SupportedFrameRates { get; } = Enumerable.Range( 1, 120 )
		.Where( x => TickRate % x == 0 )
		.ToImmutableArray();

	public static MovieTime FromTicks( int ticks ) => new MovieTime( ticks );

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

	public static MovieTime Lerp( MovieTime a, MovieTime b, double fraction )
	{
		return fraction <= 0d ? a : fraction >= 1d ? b : FromTicks( (int)(a.Ticks + (b.Ticks - a.Ticks) * fraction) );
	}

	private readonly int _ticks;

	public int Ticks => _ticks;

	public bool IsZero => _ticks == 0;
	public bool IsNegative => _ticks < 0;

	public double TotalSeconds => (double)_ticks / TickRate;

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

	public int GetFrameCount( int frameRate )
	{
		return (int)((long)_ticks * frameRate / TickRate);
	}

	public int GetFrameCount( int frameRate, out MovieTime remainder )
	{
		var frameCount = GetFrameCount( frameRate );

		remainder = FromTicks( _ticks - frameCount * frameRate );

		return frameCount;
	}

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

	public bool Equals( MovieTime other ) => _ticks == other._ticks;
	public int CompareTo( MovieTime other ) => _ticks.CompareTo( other._ticks );

	public override bool Equals( [NotNullWhen( true )] object? obj ) => obj is MovieTime span && Equals( span );
	public override int GetHashCode() => _ticks.GetHashCode();
	public override string ToString() => TimeSpan.FromSeconds( TotalSeconds ).ToString( @"mm\:ss\.fff" );
}

public readonly record struct MovieTimeRange( MovieTime Start, MovieTime End )
{
	public static implicit operator MovieTimeRange( MovieTime time ) => new( time, time );

	public static implicit operator MovieTimeRange( (MovieTime, MovieTime) tuple ) => new( tuple.Item1, tuple.Item2 );

	[JsonIgnore]
	public MovieTime Duration => End - Start;

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

	public double GetFraction( MovieTime time )
	{
		return time <= Start ? 0f : time >= End ? 1f : (double)(time - Start).Ticks / (End - Start).Ticks;
	}
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
