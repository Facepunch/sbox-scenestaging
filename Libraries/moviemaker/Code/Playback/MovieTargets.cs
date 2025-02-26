using System;
using System.Collections;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Maps tracks in a <see cref="CompiledMovieClip"/> to game objects, components, and properties in the scene.
/// We reference tracks by <see cref="Guid"/> so tracks from different clips can bind to the same target if they share an ID.
/// </summary>
public sealed partial class MovieTargets( Scene scene ) : IEnumerable<KeyValuePair<Guid, ITrackTarget>>
{
	private readonly Dictionary<Guid, ITrackTarget> _targets = new();

	/// <summary>
	/// All track IDs we currently have a target for.
	/// </summary>
	public IEnumerable<Guid> Keys => _targets.Keys;

	/// <summary>
	/// All targets currently mapped to tracks.
	/// </summary>
	public IEnumerable<ITrackTarget> Values => _targets.Values;

	/// <summary>
	/// How many tracks are currently mapped to targets.
	/// </summary>
	public int Count => _targets.Count;

	/// <summary>
	/// Tries to find an existing property mapped to the given track, returning <see langword="null"/> if not found.
	/// This won't create new target mappings, use <see cref="Get(ITrack)"/> instead.
	/// </summary>
	public ITrackTarget? Get( Guid trackId ) => _targets!.GetValueOrDefault( trackId );

	/// <summary>
	/// Gets or creates a target that maps to the given <paramref name="track"/>.
	/// </summary>
	public ITrackTarget Get( ITrack track )
	{
		Touch( track );

		return Get( track.Id )!;
	}

	/// <summary>
	/// Tries to find a target mapped to the given track that represents a <see cref="GameObject"/> reference, returning <see langword="null"/> if not found.
	/// </summary>
	public IGameObjectReference? GetGameObject( ITrack track ) =>
		Get( track ) as IGameObjectReference;

	/// <summary>
	/// Tries to find a target mapped to the given track that represents a <see cref="Component"/> reference, returning <see langword="null"/> if not found.
	/// </summary>
	public IComponentReference? GetComponent( ITrack track ) =>
		Get( track ) as IComponentReference;

	public IMember? GetMember( ITrack track ) =>
		Get( track ) as IMember;

	public IMember<T>? GetMember<T>( ITrack track ) =>
		Get( track ) as IMember<T>;

	/// <summary>
	/// Returns an enumerator that iterates through all mapped targets.
	/// </summary>
	public IEnumerator<KeyValuePair<Guid, ITrackTarget>> GetEnumerator() => _targets.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
