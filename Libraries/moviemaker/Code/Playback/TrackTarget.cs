using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Something that is being controlled by a <see cref="MovieTrack"/>. If <see cref="IsBound"/> is true,
/// this target is connected to a live instance of something in the scene, so accessing it will affect that
/// connected instance.
/// </summary>
public interface ITrackTarget
{
	/// <summary>
	/// Name of this target. Is this is a <see cref="GameObject"/> reference, it will be that object's name.
	/// If this is a <see cref="Component"/> reference, it will be the component's type name. Otherwise, this
	/// is the name of the member being accessed in a parent property.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Value type of this target.
	/// </summary>
	Type TargetType { get; }

	/// <summary>
	/// If true, this target is connected to a real object in the scene, so can be accessed.
	/// </summary>
	bool IsBound { get; }

	/// <summary>
	/// If bound, the current value of this target in the scene.
	/// </summary>
	object? Value { get; }

	/// <summary>
	/// Component / game object / property that contains this target.
	/// </summary>
	ITrackTarget? Parent { get; }
}

public interface ISceneReference : ITrackTarget
{
	/// <summary>
	/// Optional game object target that contains this one, if from a nested track.
	/// </summary>
	new IGameObjectReference? Parent { get; }
}

/// <summary>
/// A target referencing a <see cref="GameObject"/> in the scene.
/// </summary>
public interface IGameObjectReference : ISceneReference
{
	public new GameObject? Value { get; }
}

/// <summary>
/// A target referencing a <see cref="Component"/> in the scene.
/// </summary>
public interface IComponentReference : ISceneReference
{
	public new Component? Value { get; }
}

/// <summary>
/// A target referencing a member property or field of another target.
/// </summary>
public interface IMember : ITrackTarget
{
	/// <summary>
	/// Target that this member belongs to.
	/// </summary>
	new ITrackTarget Parent { get; }

	/// <summary>
	/// False if this member is readonly.
	/// </summary>
	bool CanWrite { get; }

	/// <summary>
	/// If bound, gets or sets the current value of this member.
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
