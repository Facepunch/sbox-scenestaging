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

	public MovieTime Clamp( MovieTimeRange? range )
	{
		return range is { Start: var start, End: var end }
			? Max( start, Min( end, this ) )
			: this;
	}

	public MovieTime Floor( MovieTime gridInterval )
	{
		if ( gridInterval.Ticks <= 0 ) return this;

		return FromTicks( Ticks / gridInterval.Ticks * gridInterval.Ticks );
	}

	public MovieTime Round( MovieTime gridInterval )
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
		var index = GetFrameIndex( frameRate );

		remainder = this - FromFrames( index, frameRate );

		return index;
	}

	public int GetFrameIndex( MovieTime frameInterval )
	{
		return (int)((long)_ticks / frameInterval.Ticks);
	}

	public int GetFrameIndex( MovieTime frameInterval, out MovieTime remainder )
	{
		var index = GetFrameIndex( frameInterval );

		remainder = this - frameInterval * index;

		return index;
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

	public float GetFraction( MovieTime time )
	{
		return time <= 0d ? 0f : time >= this ? 1f : (float)time.Ticks / Ticks;
	}

	public bool Equals( MovieTime other ) => _ticks == other._ticks;
	public int CompareTo( MovieTime other ) => _ticks.CompareTo( other._ticks );

	public override bool Equals( [NotNullWhen( true )] object? obj ) => obj is MovieTime span && Equals( span );
	public override int GetHashCode() => _ticks.GetHashCode();
	public override string ToString()
	{
		var timeSpan = TimeSpan.FromSeconds( TotalSeconds );
		return timeSpan.TotalHours < 1
			? timeSpan.ToString( @"mm\:ss\.fff" )
			: timeSpan.ToString( @"hh\:mm\:ss\.fff" );
	}

	#region Operators

	public static implicit operator MovieTime( float seconds ) => FromSeconds( seconds );
	public static implicit operator MovieTime( double seconds ) => FromSeconds( seconds );

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
