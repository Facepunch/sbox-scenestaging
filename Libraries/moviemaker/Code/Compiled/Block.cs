using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public abstract record Block( MovieTimeRange TimeRange ) : ValidatedRecord, IBlock;

/// <inheritdoc cref="IActionBlock"/>
public sealed record ActionBlock( MovieTimeRange TimeRange ) : Block( TimeRange ), IActionBlock;

public abstract record PropertyBlock<T>( MovieTimeRange TimeRange ) : Block( TimeRange ), IPropertyBlock<T>
{
	[JsonIgnore]
	public Type PropertyType => typeof(T);

	public abstract T GetValue( MovieTime time );

	object? IPropertyBlock.GetValue( MovieTime time ) => GetValue( time );
}

/// <inheritdoc cref="IConstantBlock{T}"/>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Value">Constant value.</param>
public sealed record ConstantBlock<T>( MovieTimeRange TimeRange, T Value )
	: PropertyBlock<T>( TimeRange ), IConstantBlock<T>
{
	public override T GetValue( MovieTime time ) => Value;

	object? IConstantBlock.Value => Value;
}

/// <inheritdoc cref="ISampleBlock{T}"/>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Offset">Time offset of the first sample.</param>
/// <param name="SampleRate">How many samples per second.</param>
/// <param name="Samples">Raw sample values.</param>
public sealed record SampleBlock<T>( MovieTimeRange TimeRange, MovieTime Offset, int SampleRate, params ImmutableArray<T> Samples )
	: PropertyBlock<T>( TimeRange ), ISampleBlock<T>
{
	private ReadOnlyListWrapper<T, object?>? _samplesWrapper;

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

	IReadOnlyList<object?> ISampleBlock.Samples => _samplesWrapper ??= new( Samples );
	IReadOnlyList<T> ISampleBlock<T>.Samples => Samples;
}
