using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// How to interpret values measured between samples in a <see cref="SamplesData{T}"/>.
/// </summary>
public enum SampleInterpolationMode
{
	/// <summary>
	/// Use the previous value.
	/// </summary>
	None,

	/// <summary>
	/// Linearly interpolate between values.
	/// </summary>
	Linear
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals, aiming to be quick
/// to read from when playing back animations.
/// </summary>
public interface ISamplesData : IMovieBlockValueData
{
	/// <summary>
	/// How many samples per second.
	/// </summary>
	int SampleRate { get; }

	/// <summary>
	/// How to interpret values measured between samples.
	/// </summary>
	SampleInterpolationMode Interpolation { get; }

	/// <summary>
	/// Raw sample values.
	/// </summary>
	IReadOnlyList<object?> Samples { get; }

	/// <summary>
	/// Total duration of the sampled signal.
	/// </summary>
	MovieTime Duration { get; }
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals, aiming to be quick
/// to read from when playing back animations.
/// </summary>
/// <typeparam name="T">Value type.</typeparam>
/// <param name="SampleRate">How many samples per second.</param>
/// <param name="Interpolation">How to interpret values measured between samples.</param>
/// <param name="Samples">Array of raw sample values.</param>
public sealed record SamplesData<T>(
	int SampleRate,
	SampleInterpolationMode Interpolation,
	IReadOnlyList<T> Samples,
	MovieTime FirstSampleTime = default )
	: ISamplesData, IMovieBlockValueData<T>
{
	private readonly IInterpolator<T>? _interpolator = Interpolation is not SampleInterpolationMode.None ? Interpolator.GetDefault<T>() : null;

	/// <summary>
	/// Total duration of the sampled signal.
	/// </summary>
	[JsonIgnore]
	public MovieTime Duration => MovieTime.FromFrames( Samples.Count, SampleRate );

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

		if ( _interpolator is null )
		{
			return x0;
		}

		var t = Interpolation.Apply( (float)(remainder.TotalSeconds * SampleRate) );
		var x1 = Samples[i1];

		return _interpolator.Interpolate( x0, x1, t );
	}

	public IMovieBlockValueData<T> Slice( MovieTimeRange timeRange )
	{
		if ( Samples.Count == 0 || timeRange.Start.IsZero && timeRange.End == Duration ) return this;

		timeRange -= FirstSampleTime;

		var i0 = timeRange.Start.GetFrameIndex( SampleRate, out var remainder );
		var i1 = timeRange.End.GetFrameCount( SampleRate );

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

		return new SamplesData<T>( SampleRate, Interpolation, Samples.Slice( i0, i1 - i0 ), firstSampleTime );
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

	public SamplesData<T> Resample( int sampleRate )
	{
		var sampleCount = Duration.GetFrameCount( sampleRate );
		var samples = new T[sampleCount];

		Sample( samples, (MovieTime.Zero, Duration), sampleRate );

		return new SamplesData<T>( sampleRate, Interpolation, samples );
	}

	Type IMovieBlockValueData.ValueType => typeof( T );

	private IReadOnlyList<object?>? _untypedList;
	IReadOnlyList<object?> ISamplesData.Samples => _untypedList ??= Samples.Cast<object?>().ToImmutableList();
	object? IMovieBlockValueData.GetValue( MovieTime time ) => GetValue( time );

	public override string ToString()
	{
		return $"[{SampleRate}, {Duration}, {FirstSampleTime}, {(_interpolator is null ? "null" : Interpolation.ToString())}]";
	}
}

internal static class SamplesExtensions
{
	public static float Apply( this SampleInterpolationMode mode, float t ) => mode switch
	{
		SampleInterpolationMode.Linear => t,
		_ => 0f
	};
}
