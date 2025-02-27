using System;
using System.Collections;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Maps <see cref="ITrack"/>s to <see cref="ITarget"/>s in a scene, representing game objects, components, and properties.
/// We reference tracks by <see cref="Guid"/> so tracks from different clips can map to the same target if they share an ID.
/// </summary>
public sealed partial class TrackTargetMap( Scene scene ) : IEnumerable<KeyValuePair<Guid, ITarget>>
{
	private readonly Dictionary<Guid, ITarget> _targets = new();

	/// <summary>
	/// All track IDs we currently have a target for.
	/// </summary>
	public IEnumerable<Guid> Keys => _targets.Keys;

	/// <summary>
	/// All targets currently mapped to tracks.
	/// </summary>
	public IEnumerable<ITarget> Values => _targets.Values;

	/// <summary>
	/// How many tracks are currently mapped to targets.
	/// </summary>
	public int Count => _targets.Count;

	/// <summary>
	/// Bind the given track to a particular <paramref name="gameObject"/> reference.
	/// This mapping will be serialized with this map.
	/// </summary>
	public void SetReference( Guid trackId, GameObject? gameObject )
	{
		_gameObjectMap[trackId] = gameObject;

		UpdateReference( trackId, gameObject );
	}

	/// <summary>
	/// Bind the given track to a particular <paramref name="component"/> reference.
	/// This mapping will be serialized with this map.
	/// </summary>
	public void SetReference( Guid trackId, Component? component )
	{
		_componentMap[trackId] = component;

		UpdateReference( trackId, component );
	}

	/// <summary>
	/// Bind the given <paramref name="track"/> to a particular <paramref name="gameObject"/> reference.
	/// This mapping will be serialized with this map.
	/// </summary>
	public void SetReference( IReferenceTrack track, GameObject? gameObject )
	{
		if ( track.TargetType != typeof( GameObject ) )
		{
			throw new ArgumentException( $"Expected a {nameof( GameObject )} track.", nameof( track ) );
		}

		GetOrCreate( track );
		SetReference( track.Id, gameObject );
	}

	/// <summary>
	/// Bind the given <paramref name="track"/> to a particular <paramref name="component"/> reference.
	/// This mapping will be serialized with this map.
	/// </summary>
	public void SetReference( IReferenceTrack track, Component? component )
	{
		if ( !track.TargetType.IsAssignableTo( typeof( Component ) ) )
		{
			throw new ArgumentException( $"Expected a {nameof( Component )} track.", nameof( track ) );
		}

		if ( component is not null && !track.TargetType.IsInstanceOfType( component ) )
		{
			throw new ArgumentException( $"Expected a {track.TargetType} instance.", nameof( component ) );
		}

		GetOrCreate( track );
		SetReference( track.Id, component );
	}

	/// <summary>
	/// Tries to find an existing target mapped to the given track, returning <see langword="null"/> if not found.
	/// This won't create new targets, use <see cref="GetOrCreate"/> instead.
	/// </summary>
	[Pure]
	public ITarget? Get( Guid trackId ) => _targets!.GetValueOrDefault( trackId );

	/// <summary>
	/// Gets or creates a target that maps to the given <paramref name="track"/>.
	/// </summary>
	public ITarget GetOrCreate( ITrack track )
	{
		var parent = track.Parent;
		var parentTarget = parent is not null ? GetOrCreate( parent ) : null;

		if ( _targets.TryGetValue( track.Id, out var target ) )
		{
			if ( target.Parent == parentTarget && target.TargetType == track.TargetType )
			{
				return target;
			}

			// Targets with the same ID should be identical, so if they aren't we
			// should investigate.

			Log.Warning( $"Existing target for track {track.Id} doesn't match! Creating a new target." );
		}

		return _targets[track.Id] = CreateTarget( track, parentTarget );
	}

	/// <summary>
	/// For each track in the given <paramref name="clip"/> that we have a mapped property for,
	/// set the property value to whatever value is stored in that track at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( IClip clip, MovieTime time )
	{
		if ( time > clip.Duration ) return;
		if ( time < MovieTime.Zero ) return;

		using var sceneScope = scene.Push();

		foreach ( var track in clip.Tracks )
		{
			if ( track is IPropertyTrack valueTrack )
			{
				ApplyFrame( valueTrack, time );
			}
		}
	}

	/// <summary>
	/// If we have a mapped property for <paramref name="track"/>, set the property value to whatever value
	/// is stored in the track at the given <paramref name="time"/>.
	/// </summary>
	public void ApplyFrame( IPropertyTrack track, MovieTime time )
	{
		if ( !track.TryGetValue( time, out var value ) ) return;
		if ( GetOrCreate( track ) is not IProperty { IsBound: true, CanWrite: true } property ) return;

		property.Value = value;
	}

	private ITarget CreateTarget( ITrack track, ITarget? parent = null )
	{
		return track switch
		{
			// GameObject reference
			IReferenceTrack when track.TargetType == typeof(GameObject) =>
				new GameObjectReference( parent as IGameObjectReference, track.Name,
					_gameObjectMap.GetValueOrDefault( track.Id ) ),

			// Component reference
			IReferenceTrack =>
				new ComponentReference( parent as IGameObjectReference, track.TargetType,
					_componentMap.GetValueOrDefault( track.Id ) ),

			// Member property within another track
			IPropertyTrack =>
				Target.Member( parent, track.Name, track.TargetType ),

			// Invalid, return a target that never binds
			_ => Target.Unknown( parent, track.Name, track.TargetType )
		};
	}

	/// <summary>
	/// Returns an enumerator that iterates through all mapped targets.
	/// </summary>
	public IEnumerator<KeyValuePair<Guid, ITarget>> GetEnumerator() => _targets.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
