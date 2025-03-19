namespace Sandbox.MovieMaker;

#nullable enable

/// <summary>
/// Plays a <see cref="IClip"/> in a <see cref="Scene"/> to animate properties over time.
/// </summary>
[Icon( "movie" )]
public sealed class MoviePlayer : Component
{
	private MovieTime _position;
	private bool _isPlaying;

	private IMovieResource? _source;
	private IClip? _clip;
	private TrackBinder? _binder;

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
	/// </summary>
	private void UpdatePosition()
	{
		if ( !Enabled ) return;

		if ( Clip is not { } clip ) return;

		clip.Update( _position, Binder );

		UpdateAnimationPlaybackRate( clip );
	}

	protected override void OnEnabled()
	{
		UpdatePosition();
	}

	protected override void OnUpdate()
	{
		if ( !IsPlaying ) return;

		_position += MovieTime.FromSeconds( Time.Delta * TimeScale );

		// Rewind if looping
		Log.Info( $"IsLooping: {IsLooping}, Duration: {Clip?.Duration}, _position: {_position}" );

		if ( IsLooping && Clip?.Duration is { IsPositive: true } duration && _position >= duration )
		{
			_position.GetFrameIndex( duration, remainder: out _position );
		}

		UpdatePosition();
	}

	/// <summary>
	/// Set the <see cref="SkinnedModelRenderer.PlaybackRate"/> of all bound renderers.
	/// </summary>
	private void UpdateAnimationPlaybackRate( IClip clip )
	{
		var renderers = clip.Tracks
			.OfType<IReferenceTrack<SkinnedModelRenderer>>()
			.Select( x => Binder.Get( x ).Value )
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
