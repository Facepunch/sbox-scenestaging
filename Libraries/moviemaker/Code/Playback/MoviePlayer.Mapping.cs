using System;

namespace Sandbox.MovieMaker;

#nullable enable

partial class MoviePlayer
{
	/// <summary>
	/// Maps tracks in a <see cref="MovieClip"/> to objects in the scene. We reference tracks by <see cref="Guid"/>
	/// so tracks from multiple clips can bind to the same property if they share an id. These get serialized with
	/// this component.
	/// </summary>
	private readonly Dictionary<Guid, ISceneReferenceMovieProperty> _sceneRefMap = new();

	/// <summary>
	/// Maps tracks in a <see cref="MovieClip"/> to properties in the scene. These are bound automatically based on
	/// <see cref="_sceneRefMap"/>, so not serialized with this component.
	/// </summary>
	private readonly Dictionary<Guid, IMemberMovieProperty> _memberMap = new();

	/// <summary>
	/// Try to get a property that maps to the given track, returning null if not found.
	/// </summary>
	public IMovieProperty? GetProperty( MovieTrack track )
	{
		var property = (IMovieProperty)_sceneRefMap.GetValueOrDefault( track.Id ) ?? _memberMap.GetValueOrDefault( track.Id );

		if ( property is null ) return null;
		if ( property.PropertyType != track.PropertyType ) return null;

		return property;
	}

	public IMovieProperty? GetOrAutoResolveProperty( MovieTrack track )
	{
		if ( GetProperty( track ) is { } existing ) return existing;

		if ( track.Parent is null && track.PropertyType == typeof(GameObject) )
		{
			// For root GameObject tracks, create a property that can have a value filled in later.

			return _sceneRefMap[track.Id] = MovieProperty.FromGameObject( track.Name );
		}

		// Can only try to auto-resolve if we know the parent's identity

		if ( track.Parent is null || GetOrAutoResolveProperty( track.Parent ) is not { } parentProperty ) return null;

		// If we're looking for a component, find it in the containing GameObject

		if ( track.Parent.PropertyType == typeof(GameObject) && track.PropertyType.IsAssignableTo( typeof(Component) ) )
		{
			// This will attempt to auto-resolve to a component in parentProperty

			return _sceneRefMap[track.Id] = MovieProperty.FromComponentType( parentProperty, track.PropertyType );
		}

		// Otherwise must be a named member property

		return _memberMap[track.Id] = MovieProperty.FromMember( parentProperty, track.Name, track.PropertyType );
	}

	public MovieTrack? GetTrack( GameObject go )
	{
		if ( MovieClip is null ) return null;

		foreach ( var track in MovieClip.AllTracks )
		{
			if ( GetProperty( track ) is not ISceneReferenceMovieProperty property ) continue;

			// References a component instead of a GameObject
			if ( property.Component is not null ) continue;
			if ( property.GameObject == go ) return track;
		}

		return null;
	}

	public MovieTrack? GetTrack( Component cmp )
	{
		if ( MovieClip is null ) return null;

		foreach ( var track in MovieClip.AllTracks )
		{
			if ( GetProperty( track ) is not ISceneReferenceMovieProperty property ) continue;
			if ( property.Component == cmp ) return track;
		}

		return null;
	}

	public MovieTrack? GetTrack( GameObject go, string propertyPath )
	{
		return GetTrack( GetTrack( go ), propertyPath );
	}

	public MovieTrack? GetTrack( Component cmp, string propertyPath )
	{
		return GetTrack( GetTrack( cmp ), propertyPath );
	}

	private MovieTrack? GetTrack( MovieTrack? parentTrack, string propertyPath )
	{
		while ( parentTrack is not null && propertyPath.Length > 0 )
		{
			var propertyName = propertyPath;

			// TODO: Hack for anim graph parameters including periods

			if ( parentTrack.PropertyType != typeof( SkinnedModelRenderer.ParameterAccessor ) && propertyPath.IndexOf( '.' ) is var index and > -1 )
			{
				propertyName = propertyPath[..index];
				propertyPath = propertyPath[(index + 1)..];
			}
			else
			{
				propertyPath = string.Empty;
			}

			parentTrack = parentTrack.Children.FirstOrDefault( x => x.Name == propertyName );
		}

		return parentTrack;
	}

	public MovieTrack GetOrCreateTrack( GameObject go )
	{
		if ( GetTrack( go ) is { } existing ) return existing;

		var property = MovieProperty.FromGameObject( go );
		var track = MovieClip!.AddTrack( property.PropertyName, property.PropertyType );

		_sceneRefMap[track.Id] = property;

		return track;
	}

	public MovieTrack GetOrCreateTrack( Component cmp )
	{
		if ( GetTrack( cmp ) is { } existing ) return existing;

		// Nest component tracks inside the containing game object's track
		var goTrack = GetOrCreateTrack( cmp.GameObject );
		var goProperty = GetProperty( goTrack )!;

		var property = MovieProperty.FromComponent( goProperty, cmp );
		var track = MovieClip!.AddTrack( property.PropertyName, property.PropertyType, goTrack );

		_sceneRefMap[track.Id] = property;

		return track;
	}

	public MovieTrack GetOrCreateTrack( GameObject go, string propertyPath )
	{
		if ( GetTrack( go, propertyPath ) is { } existing ) return existing;

		// Nest property tracks inside the containing GameObject's track

		return GetOrCreateTrack( GetOrCreateTrack( go ), propertyPath );
	}

	public MovieTrack GetOrCreateTrack( Component cmp, string propertyPath )
	{
		if ( GetTrack( cmp, propertyPath ) is { } existing ) return existing;

		// Nest property tracks inside the containing Component's track

		return GetOrCreateTrack( GetOrCreateTrack( cmp ), propertyPath );
	}

	public MovieTrack GetOrCreateTrack( MovieTrack parentTrack, string propertyPath )
	{
		var parentProperty = GetProperty( parentTrack )!;

		while ( propertyPath.Length > 0 )
		{
			var propertyName = propertyPath;

			// TODO: Hack for anim graph parameters including periods

			if ( parentTrack.PropertyType != typeof(SkinnedModelRenderer.ParameterAccessor) && propertyPath.IndexOf( '.' ) is var index and > -1 )
			{
				propertyName = propertyPath[..index];
				propertyPath = propertyPath[(index + 1)..];
			}
			else
			{
				propertyPath = string.Empty;
			}

			(parentTrack, parentProperty) = GetOrCreateTrack( parentTrack, parentProperty, propertyName );
		}

		return parentTrack;
	}

	private (MovieTrack Track, IMovieProperty Property) GetOrCreateTrack( MovieTrack parentTrack, IMovieProperty parentProperty, string propertyName )
	{
		if ( parentTrack.Children.FirstOrDefault( x => x.Name == propertyName ) is { } existingTrack )
		{
			if ( GetProperty( existingTrack ) is not IMemberMovieProperty existingProperty )
			{
				_memberMap[existingTrack.Id] = existingProperty = MovieProperty.FromMember( parentProperty, propertyName, existingTrack.PropertyType )!;
			}

			return (existingTrack, existingProperty);
		}

		var property = MovieProperty.FromMember( parentProperty, propertyName, null );
		var track = MovieClip!.AddTrack( property.PropertyName, property.PropertyType, parentTrack );

		_memberMap[track.Id] = property;

		return (track, property);
	}
}
