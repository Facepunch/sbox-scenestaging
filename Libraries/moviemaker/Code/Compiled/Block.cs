using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public abstract record Block( [property: JsonPropertyOrder( -1 )] MovieTimeRange TimeRange );

/// <summary>
/// Unused, will describe starting / stopping an action in the scene.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public sealed record ActionBlock( MovieTimeRange TimeRange ) : Block( TimeRange );

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="PropertyType">Property value type, must match <see cref="Track.TargetType"/>.</param>
public abstract record PropertyBlock( MovieTimeRange TimeRange, [property: JsonIgnore] Type PropertyType ) : Block( TimeRange )
{
	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	public object? GetValue( MovieTime time ) => OnGetValue( time );

	protected abstract object? OnGetValue( MovieTime time );
}

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// Typed version of <see cref="PropertyBlock"/>.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
public abstract record PropertyBlock<T>( MovieTimeRange TimeRange ) : PropertyBlock( TimeRange, typeof(T) )
{
	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	public new abstract T GetValue( MovieTime time );

	protected sealed override object? OnGetValue( MovieTime time ) => GetValue( time );
}

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Value">Constant value.</param>
public sealed record ConstantBlock<T>( MovieTimeRange TimeRange, T Value )
	: PropertyBlock<T>( TimeRange )
{
	public override T GetValue( MovieTime time ) => Value;
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Offset">Time offset of the first sample.</param>
/// <param name="SampleRate">How many samples per second.</param>
/// <param name="Samples">Raw sample values.</param>
public sealed record SampleBlock<T>( MovieTimeRange TimeRange, MovieTime Offset, int SampleRate, params ImmutableArray<T> Samples )
	: PropertyBlock<T>( TimeRange )
{
	private readonly bool _validated = Validate( Samples );

#pragma warning disable SB3000
	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();
#pragma warning restore SB3000

	public override T GetValue( MovieTime time )
	{
		var localTime = time.Clamp( TimeRange ) - TimeRange.Start - Offset;

		var i0 = localTime.GetFrameIndex( SampleRate, out var remainder );
		var i1 = i0 + 1;

		if ( i0 < 0 ) return Samples[0];
		if ( i1 >= Samples.Length ) return Samples[^1];

		var x0 = Samples[i0];

		if ( _interpolator is null )
		{
			return x0;
		}

		var t = (float)(remainder.TotalSeconds * SampleRate);
		var x1 = Samples[i1];

		return _interpolator.Interpolate( x0, x1, t );
	}

	private static bool Validate( ImmutableArray<T> samples )
	{
		if ( samples.IsDefaultOrEmpty )
		{
			throw new ArgumentException( "Expected at least one sample.", nameof(Samples) );
		}

		return true;
	}
}
