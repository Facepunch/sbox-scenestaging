using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker.Controllers;

namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Plays a <see cref="IClip"/> in a <see cref="Scene"/> to animate properties over time.
/// </summary>
[Icon( "live_tv" )]
public sealed class MoviePlayer : Component
{
	private MovieTime _position;
	private bool _isPlaying;

	private IMovieResource? _source;
	private IClip? _clip;
	private TrackBinder? _binder;

	/// <summary>
	/// Which components in scene have special handling while being controlled.
	/// </summary>
	private readonly Dictionary<Component, IComponentDirector?> _activeComponents = new();

	/// <summary>
	/// Maps <see cref="ITrack"/>s to game objects, components, and property <see cref="ITrackTarget"/>s in the scene.
	/// </summary>
	[Property, Hide]
	public TrackBinder Binder => _binder ??= new TrackBinder( Scene );

	/// <summary>
	/// Contains a <see cref="IClip"/> to play. Can be a <see cref="MovieResource"/> or <see cref="EmbeddedMovieResource"/>.
	/// </summary>
	[Property, Hide]
	public IMovieResource? Resource
	{
		get => _source;
		set
		{
			_clip = null;
			_source = value;
			UpdatePosition();
		}
	}

	[JsonIgnore, Property, Title( "Movie" ), Group( "Source" ), Order( -100 ), HideIf( nameof(IsEmbedded), true )]
	private MovieResource? InspectorResource
	{
		get => Resource as MovieResource;
		set => Resource = value;
	}

	private bool IsEmbedded => Resource is EmbeddedMovieResource;

	public IClip? Clip
	{
		get => _clip ?? _source?.Compiled;
		set
		{
			_clip = value;
			UpdatePosition();
		}
	}

	[Property, Group( "Playback" )]
	public bool IsPlaying
	{
		get => _isPlaying;
		set
		{
			if ( _isPlaying == value ) return;

			_isPlaying = value;
			UpdatePosition();
		}
	}

	[Property, Group( "Playback" )]
	public bool IsLooping { get; set; }

	[Property, Group( "Playback" ), Range( 0f, 2f, 0.1f )]
	public float TimeScale { get; set; } = 1f;

	public MovieTime Position
	{
		get => _position;
		set
		{
			_position = value;
			UpdatePosition();
		}
	}

	[Property, Group( "Playback" ), Title( "Position" )]
	public float PositionSeconds
	{
		get => (float)Position.TotalSeconds;
		set => Position = MovieTime.FromSeconds( value );
	}

	private MovieTime? _lastPosition;
	private readonly HashSet<Component> _inactiveComponents = new();

	/// <summary>
	/// Apply the movie clip to the scene at the current time position.
	/// </summary>
	private void UpdatePosition()
	{
		if ( !Enabled ) return;

		if ( Clip is not { } clip ) return;

		SetActiveComponents( Binder.GetActive<Component>( clip, _position ) );

		clip.Update( _position, Binder );

		var delta = _position - (_lastPosition ?? _position);
		_lastPosition = _position;

		UpdateActiveComponents( delta );
	}

	/// <summary>
	/// Set which components are currently being directed by this player. This will call <see cref="IComponentDirector.Start"/>
	/// for components that weren't already being directed, and <see cref="IComponentDirector.Stop"/> on ones that were being
	/// directed and aren't any more.
	/// </summary>
	internal void SetActiveComponents( IEnumerable<Component> components )
	{
		_inactiveComponents.Clear();

		foreach ( var component in _activeComponents.Keys )
		{
			_inactiveComponents.Add( component );
		}

		foreach ( var component in components )
		{
			AddActiveComponent( component );
		}

		foreach ( var component in _inactiveComponents )
		{
			if ( _activeComponents.Remove( component, out var director ) )
			{
				director?.Stop( this );
			}
		}
	}

	/// <summary>
	/// Call <see cref="IComponentDirector.Update"/> on all components being directed by this player.
	/// </summary>
	public void UpdateActiveComponents( MovieTime deltaTime )
	{
		foreach ( var director in _activeComponents.Values )
		{
			director?.Update( this, deltaTime );
		}
	}

	private void AddActiveComponent( Component component )
	{
		if ( !_activeComponents.TryGetValue( component, out var director ) )
		{
			director = ComponentDirector.Create( component );

			_activeComponents.Add( component, director );

			director?.Start( this );
		}

		if ( !_inactiveComponents.Remove( component ) ) return;
		if ( director?.AutoDirectedComponents is not { } references ) return;

		foreach ( var reference in references )
		{
			AddActiveComponent( reference );
		}
	}

	protected override void OnEnabled()
	{
		UpdatePosition();
	}

	protected override void OnDisabled()
	{
		SetActiveComponents( Enumerable.Empty<Component>() );
	}

	protected override void OnDestroy()
	{
		SetActiveComponents( Enumerable.Empty<Component>() );
	}

	protected override void OnUpdate()
	{
		if ( !IsPlaying ) return;

		_position += MovieTime.FromSeconds( Time.Delta * TimeScale );

		// Rewind if looping

		if ( IsLooping && Clip?.Duration is { IsPositive: true } duration && _position >= duration )
		{
			_position.GetFrameIndex( duration, remainder: out _position );
		}

		UpdatePosition();
	}
}
