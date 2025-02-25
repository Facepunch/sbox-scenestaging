using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
public interface IMovieBlock
{
	/// <summary>
	/// Start and end time of this block.
	/// </summary>
	MovieTimeRange TimeRange { get; }

	/// <summary>
	/// Track data for this block. Either a constant, sample array, or invoked action information.
	/// </summary>
	IBlockData Data { get; }
}

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
/// <param name="TimeRange">Start and end time of this block.</param>
/// <param name="Data">Track data for this block. Either a constant, sample array, or invoked action information.</param>
public readonly record struct MovieBlock( MovieTimeRange TimeRange, IBlockData Data ) : IMovieBlock;

/// <summary>
/// Base interface for action or value block data.
/// </summary>
public interface IBlockData;

/// <summary>
/// Interface for data describing how a track animates a property during a <see cref="MovieBlock"/>.
/// </summary>
public interface IValueData : IBlockData
{
	/// <summary>
	/// Property value type, must match <see cref="MovieTrack.PropertyType"/>.
	/// </summary>
	Type ValueType { get; }

	/// <summary>
	/// Samples the signal at the given <paramref name="time"/>, where <c>0</c> will return the first sample.
	/// </summary>
	object? GetValue( MovieTime time );
}

/// <summary>
/// Typed version of <see cref="IValueData"/>.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
public interface IValueData<out T> : IValueData
{
	new T GetValue( MovieTime time );
}
