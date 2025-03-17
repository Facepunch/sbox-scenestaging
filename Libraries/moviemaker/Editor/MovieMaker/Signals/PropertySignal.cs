using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Describes a value that changes over time.
/// </summary>
public interface IPropertySignal
{
	/// <summary>
	/// What type of value does this signal describe?
	/// </summary>
	Type PropertyType { get; }

	/// <summary>
	/// What value does this signal have at the given time?
	/// </summary>
	object? GetValue( MovieTime time );
}

/// <inheritdoc cref="IPropertySignal{T}"/>
// ReSharper disable once TypeParameterCanBeVariant
public interface IPropertySignal<T> : IPropertySignal
{
	/// <inheritdoc cref="IPropertySignal.GetValue"/>
	new T GetValue( MovieTime time );

	object? IPropertySignal.GetValue( MovieTime time ) => GetValue( time );
	Type IPropertySignal.PropertyType => typeof(T);
}

/// <summary>
/// A <see cref="IPropertySignal{T}"/> that can be composed with <see cref="PropertyOperation{T}"/>s,
/// and stored in a <see cref="IPropertyBlock{T}"/>.
/// </summary>
public abstract partial record PropertySignal<T> : IPropertySignal<T>
{
	public abstract T GetValue( MovieTime time );

	public PropertySignal<T> Reduce( MovieTime offset, MovieTime? start, MovieTime? end )
	{
		if ( start >= end )
		{
			return GetValue( start.Value );
		}

		return OnReduce( offset, start, end );
	}

	protected abstract PropertySignal<T> OnReduce( MovieTime offset, MovieTime? start, MovieTime? end );
}

/// <summary>
/// Extension methods for creating and composing <see cref="IPropertySignal"/>s.
/// </summary>
// ReSharper disable once UnusedMember.Global
public static partial class PropertySignalExtensions
{
	public static PropertySignal<T> Reduce<T>( this PropertySignal<T> signal ) =>
		signal.Reduce( default, null, null );

	public static PropertySignal<T> Reduce<T>( this PropertySignal<T> signal, MovieTimeRange timeRange ) =>
		signal.Reduce( default, timeRange.Start, timeRange.End );

	public static PropertySignal<T> Reduce<T>( this PropertySignal<T> signal, MovieTime offset, MovieTimeRange timeRange ) =>
		signal.Reduce( offset, timeRange.Start, timeRange.End );
}
