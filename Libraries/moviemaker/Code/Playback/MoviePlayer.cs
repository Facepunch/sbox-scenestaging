using System;

namespace Sandbox.MovieMaker;

#nullable enable

[Icon( "movie" )]
public sealed partial class MoviePlayer : Component
{
	private MovieClip? _embeddedClip;
	private MovieFile? _referencedClip;

	private MovieTime _position;
	private bool _isPlaying;

	[Property, Group( "Source" ), Hide]
	public MovieClip? EmbeddedClip
	{
		get => _embeddedClip;
		set
		{
			_embeddedClip = value;
			_referencedClip = value is not null ? null : _referencedClip;

			UpdatePosition();
		}
	}

	[Property, Group( "Source" ), Title( "Movie File" )]
	public MovieFile? ReferencedClip
	{
		get => _referencedClip;
		set
		{
			_referencedClip = value;
			_embeddedClip = value is not null ? null : _embeddedClip;

			UpdatePosition();
		}
	}

	public MovieClip? MovieClip => _embeddedClip ?? _referencedClip?.Clip;

	[Property, Group( "Playback" )]
	public bool IsPlaying
	{
		get => _isPlaying;
		set
		{
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

	/// <summary>
	/// Apply the movie clip to the scene at the current time position.
	/// If we reach the end, check <see cref="IsLooping"/> to either jump back to the start, or stop playback.
	/// </summary>
	private void UpdatePosition()
	{
		if ( MovieClip is null ) return;

		ApplyFrame( _position );
		UpdateModels( _position );
	}

	public IEnumerable<GameObject> GetControlledGameObjects()
	{
		if ( MovieClip is not { } clip ) return [];

		return _sceneRefMap
			.Where( x => x.Value.IsBound && x.Value.PropertyType == typeof(GameObject) && clip.GetTrack( x.Key ) is not null )
			.Select( x => x.Value.GameObject )
			.OfType<GameObject>();
	}

	public IEnumerable<T> GetControlledComponents<T>()
		where T : class
	{
		if ( MovieClip is not { } clip ) return [];

		return _sceneRefMap
			.Where( x => x.Value.IsBound && x.Value.PropertyType == typeof(T) && clip.GetTrack( x.Key ) is not null )
			.Select( x => x.Value.Component )
			.OfType<T>();
	}

	public void ApplyFrame( MovieTime time )
	{
		if ( MovieClip is not { } clip || _sceneRefMap.Count == 0 ) return;
		if ( time > clip.Duration ) return;
		if ( time < MovieTime.Zero ) return;

		using var sceneScope = Scene.Push();

		foreach ( var track in clip.RootTracks )
		{
			ApplyFrame( track, time );
		}
	}

	private MovieTime _lastModelPosition;

	public void UpdateModels( MovieTime time )
	{
		// Negative deltas aren't supported :(

		var dt = Math.Min( (float)(time - _lastModelPosition).Absolute.TotalSeconds, 1f );

		_lastModelPosition = time;

		foreach ( var renderer in GetControlledComponents<SkinnedModelRenderer>() )
		{
			if ( renderer.SceneModel is not { } model ) continue;

			if ( !Scene.IsEditor )
			{
				renderer.PlaybackRate = TimeScale;
			}
			else
			{
				if ( dt > 0f )
				{
					model.PlaybackRate = 1f;
					model.Update( dt );
				}

				model.PlaybackRate = 0f;
			}
		}
	}

	internal void ApplyFrame( MovieTrack track, MovieTime time )
	{
		if ( track.GetBlock( time ) is { } block )
		{
			ApplyFrame( track, block, time );
		}

		foreach ( var child in track.Children )
		{
			ApplyFrame( child, time );
		}
	}

	public void ApplyFrame( MovieTrack track, IMovieBlock block, MovieTime time )
	{
		if ( block.Data is not IMovieBlockValueData valueData ) return;
		if ( GetOrAutoResolveProperty( track ) is not { IsBound: true } property ) return;

		property.Value = valueData.GetValue( time - block.TimeRange.Start );
	}

	protected override void OnEnabled()
	{
		UpdatePosition();
	}

	protected override void OnUpdate()
	{
		if ( !IsPlaying || MovieClip is not { } clip ) return;

		_position += MovieTime.FromSeconds( Time.Delta * TimeScale );

		var duration = clip.Duration;

		if ( _position >= duration )
		{
			if ( IsLooping )
			{
				while ( duration.IsPositive && _position >= duration )
				{
					_position -= duration;
				}
			}
		}

		UpdatePosition();
	}
}
