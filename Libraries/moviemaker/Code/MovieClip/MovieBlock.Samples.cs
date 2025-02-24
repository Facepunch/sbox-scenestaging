using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// This block contains an array of values sampled at uniform intervals, aiming to be quick
/// to read from when playing back animations.
/// </summary>
public interface ISamplesData : IMovieBlockValueData, ITimeRanged
{
	/// <summary>
	/// How many samples per second.
	/// </summary>
	int SampleRate { get; }

	/// <summary>
	/// Raw sample values.
	/// </summary>
	IReadOnlyList<object?> Samples { get; }
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals, aiming to be quick
/// to read from when playing back animations.
/// </summary>
/// <typeparam name="T">Value type.</typeparam>
/// <param name="SampleRate">How many samples per second.</param>
/// <param name="Samples">Array of raw sample values.</param>
/// <param name="FirstSampleTime">Time offset of the first sample in the list.</param>
public sealed record SamplesData<T>(
	int SampleRate,
	IReadOnlyList<T> Samples,
	MovieTime FirstSampleTime = default )
	: ISamplesData, IMovieBlockValueData<T>
{
#pragma warning disable SB3000
	private static IInterpolator<T>? Interpolator { get; } = MovieMaker.Interpolator.GetDefault<T>();
#pragma warning restore SB3000

	/// <summary>
	/// Time range covered by the samples.
	/// </summary>
	[JsonIgnore]
	public MovieTimeRange TimeRange => (FirstSampleTime, FirstSampleTime + MovieTime.FromFrames( Samples.Count, SampleRate ));

	/// <summary>
	/// Samples the signal at the given <paramref name="time"/>, where <c>0</c> will return the first sample.
	/// </summary>
	public T GetValue( MovieTime time )
	{
		if ( Samples.Count == 0 ) return default!;

		time -= FirstSampleTime;

		var i0 = time.GetFrameIndex( SampleRate, out var remainder );
		var i1 = i0 + 1;

		if ( i0 < 0 ) return Samples[0];
		if ( i1 >= Samples.Count ) return Samples[^1];

		var x0 = Samples[i0];

		if ( Interpolator is null )
		{
			return x0;
		}

		var t = (float)(remainder.TotalSeconds * SampleRate);
		var x1 = Samples[i1];

		return Interpolator.Interpolate( x0, x1, t );
	}

	public IMovieBlockValueData<T> Slice( MovieTimeRange timeRange )
	{
		if ( Samples.Count == 0 || timeRange.Start.IsZero && timeRange.End == this.Duration() ) return this;

		timeRange -= FirstSampleTime;

		var i0 = timeRange.Start.GetFrameIndex( SampleRate, out var remainder );
		var i1 = i0 + timeRange.Duration.GetFrameCount( SampleRate );

		// Constants if we're off one end

		if ( i1 <= 0 ) return new ConstantData<T>( Samples[0] );
		if ( i0 >= Samples.Count ) return new ConstantData<T>( Samples[^1] );

		var firstSampleTime = -remainder;

		if ( i0 < 0 )
		{
			firstSampleTime += MovieTime.FromFrames( -i0, SampleRate );
			i0 = 0;
		}

		i1 = Math.Clamp( i1, i0, Samples.Count );

		return new SamplesData<T>( SampleRate, Samples.Slice( i0, i1 - i0 ), firstSampleTime );
	}

	IMovieBlockValueData IMovieBlockValueData.Slice( MovieTimeRange timeRange ) => Slice( timeRange );

	void IMovieBlockValueData.Sample( Array dstSamples, int dstOffset, int sampleCount, MovieTimeRange srcTimeRange, int sampleRate )
	{
		var span = ((T[])dstSamples).AsSpan( dstOffset, sampleCount );

		Sample( span, srcTimeRange, sampleRate );
	}

	public void Sample( Span<T> dstSamples, MovieTimeRange srcTimeRange, int sampleRate )
	{
		if ( Samples.Count == 0 || dstSamples.Length == 0 ) return;

		for ( var i = 0; i < dstSamples.Length; ++i )
		{
			var time = srcTimeRange.Start + MovieTime.FromFrames( i, sampleRate );

			dstSamples[i] = GetValue( time );
		}
	}

	Type IMovieBlockValueData.ValueType => typeof( T );

	private IReadOnlyList<object?>? _untypedList;
	IReadOnlyList<object?> ISamplesData.Samples => _untypedList ??= Samples.Cast<object?>().ToImmutableList();
	object? IMovieBlockValueData.GetValue( MovieTime time ) => GetValue( time );
}
