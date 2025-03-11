using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public abstract record CompiledBlock( [property: JsonPropertyOrder( -1 )] MovieTimeRange TimeRange );

/// <summary>
/// Unused, will describe starting / stopping an action in the scene.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public sealed record CompiledActionBlock( MovieTimeRange TimeRange ) : CompiledBlock( TimeRange );

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="PropertyType">Property value type, must match <see cref="CompiledTrack.TargetType"/>.</param>
public abstract record CompiledPropertyBlock( MovieTimeRange TimeRange, [property: JsonIgnore] Type PropertyType ) : CompiledBlock( TimeRange )
{
	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	public object? GetValue( MovieTime time ) => OnGetValue( time );

	protected abstract object? OnGetValue( MovieTime time );
}

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// Typed version of <see cref="CompiledPropertyBlock"/>.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
public abstract record CompiledPropertyBlock<T>( MovieTimeRange TimeRange ) : CompiledPropertyBlock( TimeRange, typeof(T) )
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
public sealed record CompiledConstantBlock<T>( MovieTimeRange TimeRange, T Value )
	: CompiledPropertyBlock<T>( TimeRange )
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
public sealed record CompiledSampleBlock<T>( MovieTimeRange TimeRange, MovieTime Offset, int SampleRate, ImmutableArray<T> Samples )
	: CompiledPropertyBlock<T>( TimeRange )
{
	private readonly bool _validated = Validate( Samples );

#pragma warning disable SB3000
	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();
#pragma warning restore SB3000

	public override T GetValue( MovieTime time ) =>
		Samples.Sample( time.Clamp( TimeRange ) - TimeRange.Start - Offset, SampleRate, _interpolator );

	private static bool Validate( ImmutableArray<T> samples )
	{
		if ( samples.IsDefaultOrEmpty )
		{
			throw new ArgumentException( "Expected at least one sample.", nameof(Samples) );
		}

		return true;
	}
}
