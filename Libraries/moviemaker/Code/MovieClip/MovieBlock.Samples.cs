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
public interface ISamplesData
{
	/// <summary>
	/// Sample value type, must match <see cref="MovieTrack.PropertyType"/>.
	/// </summary>
	Type ValueType { get; }

	/// <summary>
	/// How many samples per second.
	/// </summary>
	float SampleRate { get; }

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
	float Duration { get; }

	/// <summary>
	/// Samples the signal at the given <paramref name="time"/>, where <c>0</c> will return the first sample.
	/// </summary>
	object? GetValue( float time );
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
	float SampleRate,
	SampleInterpolationMode Interpolation,
	IReadOnlyList<T> Samples )
	: MovieBlockData, ISamplesData
{
	private readonly IInterpolator<T>? _interpolator = Interpolation is not SampleInterpolationMode.None ? Interpolator.GetDefault<T>() : null;

	/// <summary>
	/// Total duration of the sampled signal.
	/// </summary>
	[JsonIgnore]
	public float Duration => SampleRate <= 0f ? float.PositiveInfinity : Samples.Count / SampleRate;

	/// <summary>
	/// Samples the signal at the given <paramref name="time"/>, where <c>0</c> will return the first sample.
	/// </summary>
	public T GetValue( float time )
	{
		if ( Samples.Count == 0 ) return default!;

		var index = time * SampleRate;
		var i0 = (int)index.Floor();
		var i1 = i0 + 1;

		if ( i0 < 0 ) return Samples[0];
		if ( i1 >= Samples.Count ) return Samples[^1];

		var x0 = Samples[i0];

		if ( _interpolator is null )
		{
			return x0;
		}

		var t = Interpolation.Apply( index - i0 );
		var x1 = Samples[i1];

		return _interpolator.Interpolate( x0, x1, t );
	}

	Type ISamplesData.ValueType => typeof( T );

	private IReadOnlyList<object?>? _untypedList;
	IReadOnlyList<object?> ISamplesData.Samples => _untypedList ??= Samples.Cast<object?>().ToImmutableList();
	object? ISamplesData.GetValue( float time ) => GetValue( time );
}

internal static class SamplesExtensions
{
	public static float Apply( this SampleInterpolationMode mode, float t ) => mode switch
	{
		SampleInterpolationMode.Linear => t,
		_ => 0f
	};
}
