using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MovieProperties
{
	/// <summary>
	/// Stores if a track has a parent / which children it has.
	/// </summary>
	private class RegisteredTrack
	{
		public Guid Id { get; }
		public string Name { get; private set; }
		public Type PropertyType { get; private set; }

		public RegisteredTrack? Parent { get; set; }
		public List<RegisteredTrack> Children { get; } = new();

		public RegisteredTrack( IMovieTrackDescription trackDescription )
		{
			Id = trackDescription.Id;
			Name = trackDescription.Name;
			PropertyType = trackDescription.PropertyType;
		}

		public bool Update( IMovieTrackDescription trackDescription )
		{
			if ( Name == trackDescription.Name && PropertyType == trackDescription.PropertyType )
			{
				return false;
			}

			Name = trackDescription.Name;
			PropertyType = trackDescription.PropertyType;

			return true;
		}
	}

	private readonly Dictionary<Guid, RegisteredTrack> _tracks = new();
	private bool _propertiesInvalid;

	public void RegisterTracks( IEnumerable<IMovieTrackDescription> tracks, IMovieTrackDescription? parent = null )
	{
		var registeredParent = GetRegisteredParent( parent );

		foreach ( var track in tracks )
		{
			RegisterTrackCore( track, registeredParent );
		}
	}

	internal void RegisterTracksRecursive( ImmutableArray<MovieTrack> tracks, MovieTrack? parent = null )
	{
		RegisterTracksRecursiveCore( tracks, GetRegisteredParent( parent ) );
	}

	private void RegisterTracksRecursiveCore( ImmutableArray<MovieTrack> tracks, RegisteredTrack? parent = null )
	{
		foreach ( var track in tracks )
		{
			RegisterTracksRecursiveCore( track.Children, RegisterTrackCore( track, parent ) );
		}
	}

	[return: NotNullIfNotNull( nameof(parent) )]
	private RegisteredTrack? GetRegisteredParent( IMovieTrackDescription? parent )
	{
		return parent is not null
			? _tracks!.GetValueOrDefault( parent.Id ) ?? throw new ArgumentException( "Parent track hasn't been registered.", nameof(parent) )
			: null;
	}

	private RegisteredTrack RegisterTrackCore( IMovieTrackDescription track, RegisteredTrack? parent )
	{
		if ( _tracks.TryGetValue( track.Id, out var mapped ) )
		{
			_propertiesInvalid |= mapped.Update( track );
		}
		else
		{
			_tracks[track.Id] = mapped = new RegisteredTrack( track );
			_propertiesInvalid = true;
		}

		if ( mapped.Parent == parent ) return mapped;

		mapped.Parent?.Children.Remove( mapped );
		mapped.Parent = parent;
		mapped.Parent?.Children.Add( mapped );

		_propertiesInvalid = true;

		return mapped;
	}

	private void UpdateProperties()
	{
		if ( !_propertiesInvalid ) return;

		_propertiesInvalid = false;

		var roots = _tracks.Values.Where( x => x.Parent is null );

		foreach ( var track in roots )
		{
			if ( _properties.TryGetValue( track.Id, out var property ) )
			{
				ResolveChildren( track, property );
			}
			else if ( CreateRoot( track ) is { } newProperty )
			{
				ResolveChildren( track, newProperty );
			}
		}
	}

	/// <summary>
	/// Resolve a root track into a property. At the moment we only support <see cref="GameObject"/>
	/// root tracks.
	/// </summary>
	private IMovieProperty? CreateRoot( RegisteredTrack track )
	{
		if ( track.PropertyType == typeof( GameObject ) )
		{
			return _properties[track.Id] = CreateReferenceProperty( track.Id, track.Name );
		}

		return null;
	}

	/// <summary>
	/// Resolve all the child properties of the given <paramref name="parentTrack"/>.
	/// </summary>
	private void ResolveChildren( RegisteredTrack parentTrack, IMovieProperty parentProperty )
	{
		foreach ( var childTrack in parentTrack.Children )
		{
			if ( !_properties.TryGetValue( childTrack.Id, out var childProperty ) )
			{
				if ( ResolveChild( parentProperty, childTrack ) is not { } resolvedProperty )
				{
					continue;
				}

				_properties[childTrack.Id] = childProperty = resolvedProperty;
			}

			ResolveChildren( childTrack, childProperty );
		}
	}

	/// <summary>
	/// Resolve the property of <paramref name="track"/> as a member of <paramref name="parentProperty"/>.
	/// </summary>
	private IMovieProperty? ResolveChild( IMovieProperty parentProperty, RegisteredTrack track )
	{
		if ( parentProperty is IGameObjectReferenceProperty parentGameObjectProperty )
		{
			// If we're looking for a game object in a game object, check its children

			if ( track.PropertyType == typeof(GameObject) )
			{
				return _properties[track.Id] = CreateReferenceProperty( track.Id, track.Name, parentGameObjectProperty );
			}

			// If we're looking for a component, find it in the containing GameObject given its type

			if ( track.PropertyType.IsAssignableTo( typeof( Component ) ) )
			{
				return _properties[track.Id] = CreateReferenceProperty( track.Id, track.PropertyType, parentGameObjectProperty );
			}
		}

		// Otherwise must be a named member property

		if ( MovieProperty.FromMember( parentProperty, track.Name, track.PropertyType ) is { } memberProperty )
		{
			return _properties[track.Id] = memberProperty;
		}

		return null;
	}
}
