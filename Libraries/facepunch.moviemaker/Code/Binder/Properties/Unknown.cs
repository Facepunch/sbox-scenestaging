using System;

namespace Sandbox.MovieMaker.Properties;

#nullable enable

/// <summary>
/// Fallback property that can never be bound.
/// </summary>
file sealed record UnknownProperty<T>( ITrackTarget Parent, string Name ) : ITrackProperty<T>
{
	public bool IsBound => false;
	public bool IsActive => false;
	public bool CanWrite => false;

	public T Value
	{
		get => default!;
		set => _ = value;
	}
}

file sealed class UnknownPropertyFactory : ITrackPropertyFactory<ITrackTarget>
{
	int ITrackPropertyFactory.Order => int.MaxValue;

	public IEnumerable<string> GetPropertyNames( ITrackTarget parent ) => Enumerable.Empty<string>();

	public Type GetTargetType( ITrackTarget parent, string name ) => typeof(Unknown);

	public ITrackProperty<T> CreateProperty<T>( ITrackTarget parent, string name ) =>
		new UnknownProperty<T>( parent, name );
}

/// <summary>
/// Dummy type for <see cref="ITrackPropertyFactory{TParent}"/> to return if it matches
/// a track, but doesn't know what target type it maps to.
/// </summary>
public abstract class Unknown
{
	private Unknown() { }
}
