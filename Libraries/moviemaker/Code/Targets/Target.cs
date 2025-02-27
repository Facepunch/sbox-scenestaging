using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// <para>
/// Something in the scene that is being controlled by an <see cref="ITrack"/>.
/// This could be a <see cref="GameObject"/> or <see cref="Component"/> reference, or a property contained
/// within another <see cref="ITarget"/>.
/// </para>
/// <para>
/// These targets are created using <see cref="TrackTargetMap.GetOrCreate"/>.
/// </para>
/// <para>
/// If <see cref="IsBound"/> is true, this target is connected to a live instance of something in the scene,
/// so accessing it will affect that connected instance.
/// </para>
/// </summary>
public interface ITarget
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
	/// Component / game object / property that contains this target, if from a nested track.
	/// </summary>
	ITarget? Parent { get; }
}

/// <summary>
/// A target referencing a <see cref="GameObject"/> or <see cref="Component"/> in the scene.
/// </summary>
public interface IReference : ITarget
{
	/// <summary>
	/// Optional game object target that contains this one, if from a nested track.
	/// </summary>
	new IGameObjectReference? Parent { get; }
}

/// <summary>
/// A target referencing a <see cref="GameObject"/> in the scene. If a track is mapped to
/// this target, its child tracks can auto-bind to components or properties contained in
/// a game object.
/// </summary>
public interface IGameObjectReference : IReference
{
	/// <summary>
	/// Game object reference in the scene that this target is bound to.
	/// </summary>
	public new GameObject? Value { get; }
}

/// <summary>
/// A target referencing a <see cref="Component"/> in the scene. If a track is mapped to
/// this target, its child tracks can auto-bind to properties of this component's type.
/// </summary>
public interface IComponentReference : IReference
{
	/// <summary>
	/// Component reference in the scene that this target is bound to.
	/// </summary>
	public new Component? Value { get; }
}

/// <summary>
/// A target referencing a member property or field of another target.
/// </summary>
public interface IProperty : ITarget
{
	/// <summary>
	/// Target that this member belongs to.
	/// </summary>
	new ITarget Parent { get; }

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
/// Typed <see cref="IProperty"/>.
/// </summary>
/// <typeparam name="T">Value type stored in the property.</typeparam>
public interface IProperty<T> : IProperty
{
	/// <inheritdoc cref="IProperty.Value"/>
	new T Value { get; set; }
}
