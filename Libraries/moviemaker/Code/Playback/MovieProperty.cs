using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A property somewhere in a scene that is being controlled by a <see cref="MovieTrack"/>.
/// </summary>
public interface IMovieProperty
{
	/// <summary>
	/// Name of this property. Is this is a <see cref="GameObject"/> reference, it will be that object's name.
	/// If this is a <see cref="Component"/> reference, it will be the component's type name. Otherwise, this
	/// is the name of the member being accessed in a parent property.
	/// </summary>
	string PropertyName { get; }

	/// <summary>
	/// Value type of this property.
	/// </summary>
	Type PropertyType { get; }

	/// <summary>
	/// If true, this property was successfully mapped to something in the scene.
	/// </summary>
	bool IsBound { get; }

	/// <summary>
	/// If bound, the current value of this property in the scene.
	/// </summary>
	object? Value { get; }
}

/// <summary>
/// A property referencing a <see cref="GameObject"/> in the scene.
/// </summary>
public interface IGameObjectReference : IMovieProperty
{
	IGameObjectReference? Parent { get; }

	public new GameObject? Value { get; set; }
}

/// <summary>
/// A property referencing a <see cref="Component"/> in the scene.
/// </summary>
public interface IComponentReference : IMovieProperty
{
	IGameObjectReference Parent { get; }

	public new Component? Value { get; set; }
}

/// <summary>
/// Movie property that represents a member inside another property.
/// </summary>
public interface IMember : IMovieProperty
{
	/// <summary>
	/// Property that this member belongs to.
	/// </summary>
	IMovieProperty Parent { get; }

	/// <summary>
	/// False if this property is readonly.
	/// </summary>
	bool CanWrite { get; }

	/// <summary>
	/// If <see cref="IMovieProperty.IsBound"/>, gets or sets the current value of this property.
	/// </summary>
	new object? Value { get; set; }
}

/// <summary>
/// Typed <see cref="IMember"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
public interface IMember<T> : IMember
{
	/// <inheritdoc cref="IMember.Value"/>
	new T Value { get; set; }
}
