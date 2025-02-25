using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Everything needed to identify a movie track.
/// </summary>
public interface IMovieTrackDescription
{
	/// <summary>
	/// ID for referencing this track. Must be unique in the containing <see cref="MovieClip"/>.
	/// </summary>
	Guid Id { get; }

	/// <summary>
	/// Property or object name, used when auto-resolving this track in a scene.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// What type of property is this track controlling.
	/// </summary>
	Type PropertyType { get; }
}

/// <summary>
/// <para>
/// Maps tracks in a <see cref="MovieClip"/> to objects and properties in the scene.
/// We reference tracks by <see cref="Guid"/> so tracks from multiple clips can bind to the same property if they share an ID.
/// This won't contain any properties until you call <see cref="RegisterTracks"/>, after which it will try to resolve those
/// tracks in the scene.
/// </para>
/// <para>
/// Root tracks in a clip represent <see cref="GameObject"/>s that should be animated by the movie. These map to
/// <see cref="IGameObjectReference"/> instances.
/// </para>
/// </summary>
public sealed partial class MovieProperties( Scene scene ) : IReadOnlyDictionary<Guid, IMovieProperty>
{
	private readonly Dictionary<Guid, IMovieProperty> _properties = new();

	/// <summary>
	/// Safe way to access <see cref="_properties"/> if you want to make sure they're up-to-date.
	/// </summary>
	private IReadOnlyDictionary<Guid, IMovieProperty> Properties
	{
		get
		{
			UpdateProperties();
			return _properties;
		}
	}

	/// <summary>
	/// How many tracks are currently mapped to properties.
	/// </summary>
	public int Count => Properties.Count;

	/// <summary>
	/// Retrieves a property with the given ID, throwing if not found.
	/// </summary>
	/// <param name="key">Track ID to look up.</param>
	public IMovieProperty this[ Guid key ] => Properties[key];

	/// <summary>
	/// Tries to find a property mapped to the given track with matching property type, returning <see langword="null"/> if not found.
	/// </summary>
	public IMovieProperty? Get( IMovieTrackDescription track ) =>
		Properties.TryGetValue( track.Id, out var property ) && property.PropertyType == track.PropertyType
			? property
			: null;

	/// <summary>
	/// Tries to find a property mapped to the given track that represents a <see cref="GameObject"/> reference, returning <see langword="null"/> if not found.
	/// </summary>
	public IGameObjectReference? GetGameObject( IMovieTrackDescription track ) =>
		Get( track ) as IGameObjectReference;

	/// <summary>
	/// Tries to find a property mapped to the given track that represents a <see cref="Component"/> reference, returning <see langword="null"/> if not found.
	/// </summary>
	public IComponentReference? GetComponent( IMovieTrackDescription track ) =>
		Get( track ) as IComponentReference;

	public IMember? GetMember( IMovieTrackDescription track ) =>
		Get( track ) as IMember;

	public IMember<T>? GetMember<T>( IMovieTrackDescription track ) =>
		Get( track ) as IMember<T>;

	/// <summary>
	/// All track IDs we have mapped properties for.
	/// </summary>
	public IEnumerable<Guid> Keys => Properties.Keys;

	/// <summary>
	/// All properties mapped to known tracks.
	/// </summary>
	public IEnumerable<IMovieProperty> Values => Properties.Values;

	/// <summary>
	/// Returns true if we have a property mapped to the given track ID.
	/// </summary>
	public bool ContainsKey( Guid key ) => Properties.ContainsKey( key );

	/// <summary>
	/// Returns true if we have a property mapped to the given track ID, outputting it as <paramref name="value"/>.
	/// </summary>
	public bool TryGetValue( Guid key, [MaybeNullWhen( false )] out IMovieProperty value ) =>
		Properties.TryGetValue( key, out value );

	/// <summary>
	/// Returns an enumerator that iterates through all mapped properties.
	/// </summary>
	public IEnumerator<KeyValuePair<Guid, IMovieProperty>> GetEnumerator() => Properties.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
