namespace Sandbox.MovieMaker;

#nullable enable

[Icon( "movie" )]
public sealed class MoviePlayer : Component
{
	private MovieTime _position;
	private bool _isPlaying;

	private IMovieSource? _source;
	private MovieClip? _lastClip;
	private MovieProperties? _properties;

	[Property, Hide]
	public MovieProperties Properties => _properties ??= new MovieProperties( Scene );

	[Property, Group( "Source" )]
	public IMovieSource? MovieSource
	{
		get => _source;
		set
		{
			_source = value;
			UpdatePosition();
		}
	}

	public MovieClip? MovieClip => MovieSource?.Clip;

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

	private void UpdateClip()
	{
		var clip = MovieClip;

		if ( _lastClip == clip ) return;

		_lastClip = clip;

		if ( clip?.RootTracks is { } tracks )
		{
			Properties.RegisterTracksRecursive( tracks );
		}
	}

	/// <summary>
	/// Apply the movie clip to the scene at the current time position.
	/// </summary>
	private void UpdatePosition()
	{
		if ( !Enabled ) return;

		UpdateClip();

		if ( MovieClip is not { } clip ) return;

		Properties.ApplyFrame( clip, _position );

		UpdateAnimationPlaybackRate( clip );
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

	/// <summary>
	/// Set the <see cref="SkinnedModelRenderer.PlaybackRate"/> of all bound renderers.
	/// </summary>
	private void UpdateAnimationPlaybackRate( MovieClip clip )
	{
		var renderers = clip.Tracks
			.Where( x => x.PropertyType == typeof(SkinnedModelRenderer) )
			.Select( x => (Properties[x] as IComponentReferenceProperty)?.Component )
			.OfType<SkinnedModelRenderer>();

		foreach ( var renderer in renderers )
		{
			if ( renderer.SceneModel is not { } model ) continue;

			// We're assuming SkinnedModelRenderer.PlaybackRate persists even if we change SceneModel.PlaybackRate,
			// so we don't stomp relative playback rates

			model.PlaybackRate = renderer.PlaybackRate * TimeScale;
		}
	}
}
