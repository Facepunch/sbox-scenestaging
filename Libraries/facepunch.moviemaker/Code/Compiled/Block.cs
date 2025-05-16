using System;
using System.Collections.Immutable;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable
/// <summary>
/// A block of time where something happens in an <see cref="ICompiledTrack"/>.
/// </summary>
public interface ICompiledBlock : ITrackBlock;

/// <summary>
/// Unused, will describe starting / stopping an action in the scene.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public sealed record CompiledActionBlock( MovieTimeRange TimeRange ) : ICompiledBlock;

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// </summary>
public interface ICompiledPropertyBlock : ICompiledBlock, IPropertyBlock;

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// Typed version of <see cref="ICompiledPropertyBlock"/>.
/// </summary>
// ReSharper disable once TypeParameterCanBeVariant
public interface ICompiledPropertyBlock<T> : ICompiledPropertyBlock, IPropertyBlock<T>;

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
