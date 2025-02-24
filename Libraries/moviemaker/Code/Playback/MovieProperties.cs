using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker;

#nullable enable

public interface IMovieTrackDescription
{
	Guid Id { get; }
	string Name { get; }
	Type PropertyType { get; }
}

/// <summary>
/// Maps tracks in a <see cref="MovieClip"/> to objects and properties in the scene.
/// We reference tracks by <see cref="Guid"/> so tracks from multiple clips can bind to the same property if they share an id.
/// </summary>
public sealed partial class MovieProperties( Scene scene ) : IReadOnlyDictionary<Guid, IMovieProperty>
{
	private readonly Dictionary<Guid, IMovieProperty> _properties = new();
	public int Count => _properties.Count;

	public IMovieProperty this[ Guid key ] =>
		_properties[key];

	/// <summary>
	/// Tries to get a property mapped to the given track with matching property type, returning <see langword="null"/> if not found.
	/// </summary>
	public IMovieProperty? this[ IMovieTrackDescription track ] =>
		_properties.TryGetValue( track.Id, out var property ) && property.PropertyType == track.PropertyType
			? property
			: null;

	/// <summary>
	/// All track IDs we have mapped properties for.
	/// </summary>
	public IEnumerable<Guid> Keys => _properties.Keys;

	/// <summary>
	/// All properties mapped to known tracks.
	/// </summary>
	public IEnumerable<IMovieProperty> Values => _properties.Values;

	public bool ContainsKey( Guid key ) =>
		_properties.ContainsKey( key );

	public bool TryGetValue( Guid key, [MaybeNullWhen( false )] out IMovieProperty value ) =>
		_properties.TryGetValue( key, out value );

	public IEnumerator<KeyValuePair<Guid, IMovieProperty>> GetEnumerator() => _properties.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
