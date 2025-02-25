using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// This block contains an array of values sampled at uniform intervals, aiming to be quick
/// to read from when playing back animations.
/// </summary>
public interface ISamplesData : IValueData
{
	/// <summary>
	/// How many samples per second.
	/// </summary>
	int SampleRate { get; }

	/// <summary>
	/// Time offset of the first sample in the list.
	/// </summary>
	MovieTime Offset { get; }

	/// <summary>
	/// Retrieves the sample at the given index.
	/// </summary>
	object? this[int index] { get; }
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals, aiming to be quick
/// to read from when playing back animations.
/// </summary>
/// <typeparam name="T">Value type.</typeparam>
public sealed class SamplesData<T> : ISamplesData, IValueData<T>
{
#pragma warning disable SB3000
	private static IInterpolator<T>? Interpolator { get; } = MovieMaker.Interpolator.GetDefault<T>();
#pragma warning restore SB3000

	/// <inheritdoc />
	public int SampleRate { get; }

	/// <summary>
	/// Values sampled at <see cref="SampleRate"/>.
	/// </summary>
	public ImmutableArray<T> Samples { get; }

	/// <inheritdoc />
	public MovieTime Offset { get; }

	/// <summary>
	/// Time range covered by the samples.
	/// </summary>
	[JsonIgnore]
	public MovieTimeRange TimeRange => (Offset, Offset + MovieTime.FromFrames( Samples.Length - 1, SampleRate ));

	[JsonConstructor]
	public SamplesData( int sampleRate, IEnumerable<T> samples, MovieTime offset = default )
	{
		SampleRate = sampleRate;
		Samples = [..samples];
		Offset = offset;

		if ( Samples.Length <= 0 )
		{
			throw new ArgumentException( "Expected at least one sample.", nameof(samples) );
		}
	}

	/// <summary>
	/// Samples the signal at the given <paramref name="time"/>, where <c>0</c> will return the first sample.
	/// </summary>
	public T GetValue( MovieTime time )
	{
		if ( Samples.Length == 0 ) return default!;

		time -= Offset;

		var i0 = time.GetFrameIndex( SampleRate, out var remainder );
		var i1 = i0 + 1;

		if ( i0 < 0 ) return Samples[0];
		if ( i1 >= Samples.Length ) return Samples[^1];

		var x0 = Samples[i0];

		if ( Interpolator is null )
		{
			return x0;
		}

		var t = (float)(remainder.TotalSeconds * SampleRate);
		var x1 = Samples[i1];

		return Interpolator.Interpolate( x0, x1, t );
	}

	Type IValueData.ValueType => typeof( T );
	object? IValueData.GetValue( MovieTime time ) => GetValue( time );
	object? ISamplesData.this[int index] => Samples[index];
}
