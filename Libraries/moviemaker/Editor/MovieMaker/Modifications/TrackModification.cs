using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Performs some kind of edit on a track given a time range selection.
/// </summary>
public interface ITrackModification;

public interface ITrackModificationOptions;

public interface ITrackModification<T> : ITrackModification
{
	/// <summary>
	/// Performs the modification on a set of blocks from a track, returning the modified blocks.
	/// </summary>
	/// <param name="original">Blocks from the source track to modify.</param>
	/// <param name="selection">Time envelope to apply the modification to.</param>
	/// <param name="options">Modification-specific options.</param>
	IEnumerable<PropertyBlock<T>> Apply( IReadOnlyList<PropertyBlock<T>> original, TimeSelection selection, ITrackModificationOptions options );
}

/// <inheritdoc cref="ITrackModification"/>
public interface ITrackModification<TValue, in TOptions> : ITrackModification<TValue>
	where TOptions : ITrackModificationOptions
{
	IEnumerable<PropertyBlock<TValue>> Apply( IReadOnlyList<PropertyBlock<TValue>> original, TimeSelection selection,
		TOptions options );

	IEnumerable<PropertyBlock<TValue>> ITrackModification<TValue>.Apply( IReadOnlyList<PropertyBlock<TValue>> original,
		TimeSelection selection, ITrackModificationOptions options ) =>
		Apply( original, selection, (TOptions)options );
}
