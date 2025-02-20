using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
public interface IConstantData : IMovieBlockValueData
{
	/// <summary>
	/// Constant value.
	/// </summary>
	object? Value { get; }
}

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
/// <param name="Value">Constant value.</param>
public sealed record ConstantData<T>( T Value ) : IConstantData, IMovieBlockValueData<T>
{
	Type IMovieBlockValueData.ValueType => typeof( T );

	object? IMovieBlockValueData.GetValue( MovieTime time ) => Value;

	public IMovieBlockValueData<T> Slice( MovieTimeRange timeRange ) => this;

	public void Sample( Span<T> dstSamples, MovieTimeRange srcTimeRange, int sampleRate ) => dstSamples.Fill( Value );

	public T GetValue( MovieTime time ) => Value;

	IMovieBlockValueData IMovieBlockValueData.Slice( MovieTimeRange timeRange ) => this;

	object? IConstantData.Value => Value;
}
