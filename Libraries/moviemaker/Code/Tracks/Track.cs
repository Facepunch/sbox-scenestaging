using System;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Maps to a <see cref="ITarget"/> in a scene, and describes how it changes over time.
/// </summary>
public interface ITrack
{
	/// <summary>
	/// ID for referencing this track. Must be unique in the containing <see cref="IClip"/>,
	/// but different clips can share tracks as long as they are identical.
	/// </summary>
	Guid Id { get; }

	/// <summary>
	/// Property or object name, used when auto-binding this track in a scene.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// What type of object or property is this track targeting.
	/// </summary>
	Type TargetType { get; }

	/// <summary>
	/// Tracks can be nested, which means child tracks can auto-bind to targets in the scene
	/// if their parent is bound.
	/// </summary>
	ITrack? Parent { get; }
}

/// <summary>
/// Maps to a <see cref="IReference"/> in a scene, which binds to a <see cref="GameObject"/>
/// or <see cref="Component"/>.
/// </summary>
public interface IReferenceTrack : ITrack;

/// <summary>
/// Unused, will describe running actions in the scene.
/// </summary>
public interface IActionTrack : IBlockTrack<IActionBlock>;

/// <summary>
/// A <see cref="ITrack"/> that has a list of <see cref="IBlock"/>s. Blocks
/// are time spans where the track wants to do something.
/// </summary>
public interface IBlockTrack : ITrack
{
	/// <summary>
	/// Smallest time range including all blocks.
	/// </summary>
	MovieTimeRange TimeRange { get; }

	/// <summary>
	/// All the blocks contained in the track.
	/// </summary>
	IReadOnlyList<IBlock> Blocks { get; }
}

/// <summary>
/// Typed <see cref="IBlockTrack"/>.
/// </summary>
/// <typeparam name="T">Block type.</typeparam>
public interface IBlockTrack<out T> : IBlockTrack
	where T : IBlock
{
	/// <inheritdoc cref="IBlockTrack.Blocks"/>
	new IReadOnlyList<T> Blocks { get; }
}

/// <summary>
/// This track controls a property in the scene.
/// </summary>
public interface IPropertyTrack : ITrack
{
	bool TryGetValue( MovieTime time, out object? value );
}

/// <inheritdoc cref="IPropertyTrack"/>
/// <typeparam name="T">Property value type, must match <see cref="ITrack.TargetType"/>.</typeparam>
public interface IPropertyTrack<T> : IPropertyTrack
{
	bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value );
}
