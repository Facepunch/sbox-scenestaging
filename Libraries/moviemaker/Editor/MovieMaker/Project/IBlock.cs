using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

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
public interface IPropertyBlock : IBlock
{
	/// <summary>
	/// Property value type, must match <see cref="ITrack.TargetType"/>.
	/// </summary>
	Type PropertyType { get; }

	/// <summary>
	/// Reads from this block at the given <paramref name="time"/>.
	/// </summary>
	object? GetValue( MovieTime time );
}

/// <summary>
/// Typed version of <see cref="IPropertyBlock"/>.
/// </summary>
/// <typeparam name="T">Property value type.</typeparam>
public interface IPropertyBlock<out T> : IPropertyBlock
{
	/// <inheritdoc cref="IPropertyBlock.GetValue"/>
	new T GetValue( MovieTime time );

	object? IPropertyBlock.GetValue( MovieTime time ) => GetValue( time );
}

/// <summary>
/// This block has a single constant value for the whole duration.
/// Useful for value types that can't be interpolated, and change infrequently.
/// </summary>
public interface IConstantBlock : IPropertyBlock
{
	/// <summary>
	/// Constant value.
	/// </summary>
	object? Value { get; }
}

/// <inheritdoc cref="IConstantBlock" />
/// <typeparam name="T">Constant value type.</typeparam>
public interface IConstantBlock<out T> : IConstantBlock, IPropertyBlock<T>
{
	/// <inheritdoc cref="IConstantBlock.Value" />
	new T Value { get; }

	object? IConstantBlock.Value => Value;
}

/// <summary>
/// This block contains an array of values sampled at uniform intervals.
/// </summary>
public interface ISampleBlock : IPropertyBlock
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
public interface ISampleBlock<out T> : ISampleBlock, IPropertyBlock<T>
{
	/// <inheritdoc cref="ISampleBlock.Samples" />
	new IReadOnlyList<T> Samples { get; }
}

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
