using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
public interface IConstantData
{
	/// <summary>
	/// Value type, must match <see cref="MovieTrack.PropertyType"/>.
	/// </summary>
	Type ValueType { get; }

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
public sealed record ConstantData<T>( T Value ) : MovieBlockData, IConstantData
{
	Type IConstantData.ValueType => typeof( T );
	object? IConstantData.Value => Value;
}
