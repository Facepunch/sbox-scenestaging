using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public interface ICompiledBlock
{
	MovieTimeRange TimeRange { get; }
}

/// <summary>
/// Unused, will describe starting / stopping an action in the scene.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public sealed record CompiledActionBlock( MovieTimeRange TimeRange ) : ICompiledBlock;

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="PropertyType">Property value type, must match <see cref="ICompiledTrack.TargetType"/>.</param>
public interface ICompiledPropertyBlock : ICompiledBlock
{
	Type PropertyType { get; }

	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	object? GetValue( MovieTime time );
}

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// Typed version of <see cref="ICompiledPropertyBlock"/>.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
// ReSharper disable once TypeParameterCanBeVariant
public interface ICompiledPropertyBlock<T> : ICompiledPropertyBlock
{
	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	public new T GetValue( MovieTime time );

	Type ICompiledPropertyBlock.PropertyType => typeof(T);
	object? ICompiledPropertyBlock.GetValue( MovieTime time ) => GetValue( time );
}

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Value">Constant value.</param>
public sealed record CompiledConstantBlock<T>( MovieTimeRange TimeRange, T Value )
	: ICompiledPropertyBlock<T>
{
	public T GetValue( MovieTime time ) => Value;
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Offset">Time offset of the first sample.</param>
/// <param name="SampleRate">How many samples per second.</param>
/// <param name="Samples">Raw sample values.</param>
public sealed partial record CompiledSampleBlock<T>( MovieTimeRange TimeRange, MovieTime Offset, int SampleRate, ImmutableArray<T> Samples )
	: ICompiledPropertyBlock<T>
{
	private readonly ImmutableArray<T> _samples = Validate( Samples );

	public ImmutableArray<T> Samples
	{
		get => _samples;
		init => _samples = Validate( value );
	}

	public T GetValue( MovieTime time ) =>
		Samples.Sample( time.Clamp( TimeRange ) - TimeRange.Start - Offset, SampleRate, _interpolator );

	private static ImmutableArray<T> Validate( ImmutableArray<T> samples )
	{
		if ( samples.IsDefaultOrEmpty )
		{
			throw new ArgumentException( "Expected at least one sample.", nameof(Samples) );
		}

		return samples;
	}

#pragma warning disable SB3000
	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();
#pragma warning restore SB3000
}
