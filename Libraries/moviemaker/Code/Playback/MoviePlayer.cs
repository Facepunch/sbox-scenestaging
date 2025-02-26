namespace Sandbox.MovieMaker;

#nullable enable

[Icon( "movie" )]
public sealed class MoviePlayer : Component
{
	private MovieTime _position;
	private bool _isPlaying;

	private IMovieSource? _source;
	private MovieTargets? _targets;

	/// <summary>
	/// Maps tracks in movie clips to game objects, components, and properties in the scene.
	/// </summary>
	[Property, Hide]
	public MovieTargets Targets => _targets ??= new MovieTargets( Scene );

	/// <summary>
	/// Contains a <see cref="CompiledMovieClip"/>. Can be a <see cref="MovieResource"/> or <see cref="EmbeddedMovieResource"/>.
	/// </summary>
	[Property, Group( "Source" )]
	public IMovieSource? Source
	{
		get => _source;
		set
		{
			_source = value;
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

		if ( Source is not { Compiled: { } clip } ) return;

		Targets.ApplyFrame( clip, _position );

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

		if ( IsLooping && Source?.Compiled?.Duration is { IsPositive: true } duration && _position >= duration )
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
			.Where( x => x.TargetType == typeof(SkinnedModelRenderer) )
			.Select( x => Targets.GetComponent( x )?.Value )
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
