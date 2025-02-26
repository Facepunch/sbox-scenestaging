using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker.Compiled;

#nullable enable

internal enum BlockKind
{
	Action,
	Constant,
	Sample
}

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
public abstract record CompiledBlock( MovieTimeRange TimeRange ) : ValidatedRecord, IBlock
{
	[JsonInclude]
	internal abstract BlockKind Kind { get; }
}

/// <inheritdoc cref="IActionBlock"/>
public sealed record ActionBlock( MovieTimeRange TimeRange ) : CompiledBlock( TimeRange ), IActionBlock
{
	internal override BlockKind Kind => BlockKind.Action;
}

/// <inheritdoc cref="IConstantBlock{T}"/>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Value">Constant value.</param>
public sealed record ConstantBlock<T>( MovieTimeRange TimeRange, T Value )
	: CompiledBlock( TimeRange ), IConstantBlock<T>
{
	internal override BlockKind Kind => BlockKind.Constant;

	public Type ValueType => typeof(T);

	public T GetValue( MovieTime time ) => Value;

	object? IValueBlock.GetValue( MovieTime time ) => Value;
	object? IConstantBlock.Value => Value;
}

/// <inheritdoc cref="ISampleBlock{T}"/>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="SampleRate">How many samples per second.</param>
/// <param name="Samples">Raw sample values.</param>
/// <param name="Offset">Time offset of the first sample.</param>
public sealed record SampleBlock<T>( MovieTimeRange TimeRange, int SampleRate, ImmutableArray<T> Samples, MovieTime Offset )
	: CompiledBlock( TimeRange ), ISampleBlock<T>
{
	internal override BlockKind Kind => BlockKind.Sample;

	private ReadOnlyListWrapper<T, object?>? _samplesWrapper;

#pragma warning disable SB3000
	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();
#pragma warning restore SB3000

	public Type ValueType => typeof(T);

	public T GetValue( MovieTime time )
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

	object? IValueBlock.GetValue( MovieTime time ) => GetValue( time );

	IReadOnlyList<object?> ISampleBlock.Samples => _samplesWrapper ??= new( Samples );
	IReadOnlyList<T> ISampleBlock<T>.Samples => Samples;
}
