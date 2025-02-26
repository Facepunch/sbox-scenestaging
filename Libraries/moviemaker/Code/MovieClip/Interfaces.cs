using System;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// A timeline of <see cref="ITrack"/>s describing properties changing over time and actions being invoked.
/// </summary>
public interface IClip
{
	/// <summary>
	/// All tracks within the clip.
	/// </summary>
	IReadOnlyList<ITrack> Tracks { get; }

	/// <summary>
	/// How long this clip takes to fully play.
	/// </summary>
	MovieTime Duration { get; }

	/// <summary>
	/// Attempts to get a track with the given <paramref name="trackId"/>.
	/// </summary>
	/// <returns>The matching track, or <see langword="null"/> if not found.</returns>
	public ITrack? GetTrack( Guid trackId );
}

/// <summary>
/// Everything needed to bind a <see cref="ITrack"/> to an <see cref="ITrackTarget"/> in a scene.
/// </summary>
public interface ITrackDescription
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
	/// What type of value is this track controlling.
	/// </summary>
	Type TargetType { get; }

	/// <summary>
	/// Tracks can be nested, which means child tracks can auto-bind to targets in the scene
	/// if their parent is bound.
	/// </summary>
	ITrackDescription? Parent { get; }
}

/// <summary>
/// Maps to a <see cref="ITrackTarget"/> in a scene. Describes how that target should change
/// over time, with time split into <see cref="Blocks"/>.
/// </summary>
public interface ITrack : ITrackDescription
{
	/// <summary>
	/// Time ranges that have actions or property values stored.
	/// </summary>
	IReadOnlyList<IBlock> Blocks { get; }

	/// <summary>
	/// Gets whichever block is in control at the given <paramref name="time"/>.
	/// </summary>
	IBlock? GetBlock( MovieTime time );
}

/// <summary>
/// A time region where something happens in a movie.
/// </summary>
public interface IBlock
{
	/// <summary>
	/// Start and end time of this block.
	/// </summary>
	MovieTimeRange TimeRange { get; }
}

/// <summary>
/// Unused, will describe starting / stopping an action in the scene.
/// </summary>
public interface IActionBlock : IBlock;

/// <summary>
/// Interface for blocks describing a property changing value over time.
/// </summary>
public interface IValueBlock : IBlock
{
	/// <summary>
	/// Property value type, must match <see cref="ITrack.TargetType"/>.
	/// </summary>
	Type ValueType { get; }

	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	object? GetValue( MovieTime time );
}

/// <summary>
/// Typed version of <see cref="IValueBlock"/>.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
public interface IValueBlock<out T> : IValueBlock
{
	/// <inheritdoc cref="IValueBlock.GetValue"/>
	new T GetValue( MovieTime time );
}

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
public interface IConstantBlock : IValueBlock
{
	/// <summary>
	/// Constant value.
	/// </summary>
	object? Value { get; }
}

/// <inheritdoc cref="IConstantBlock" />
/// <typeparam name="T">Constant value type.</typeparam>
public interface IConstantBlock<out T> : IConstantBlock, IValueBlock<T>
{
	/// <inheritdoc cref="IConstantBlock.Value" />
	new T Value { get; }
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals.
/// </summary>
public interface ISampleBlock : IValueBlock
{
	/// <summary>
	/// How many samples per second.
	/// </summary>
	int SampleRate { get; }

	/// <summary>
	/// Raw sample values.
	/// </summary>
	IReadOnlyList<object?> Samples { get; }

	/// <summary>
	/// Time offset of the first sample in the list.
	/// </summary>
	MovieTime Offset { get; }
}

/// <inheritdoc cref="ISampleBlock" />
/// <typeparam name="T">Sample value type.</typeparam>
public interface ISampleBlock<out T> : ISampleBlock, IValueBlock<T>
{
	/// <inheritdoc cref="ISampleBlock.Samples" />
	new IReadOnlyList<T> Samples { get; }
}
