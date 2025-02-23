using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable
public sealed partial class Session
{
	private bool _isPlaying;
	private bool _isLooping = true;
	private float _timeScale = 1f;

	private MovieTime? _lastPlayerPosition;
	private bool _applyNextFrame;

	private float _scrubbingTimeScale;

	private readonly Queue<(RealTimeSince TimeSince, MovieTime MovieTime)> _scrubHistory =
		new Queue<(RealTimeSince RealTime, MovieTime MovieTime)>();

	private const float ScrubHistoryWindow = 0.25f;

	public bool IsPlaying
	{
		get => IsEditorScene ? _isPlaying : Player.IsPlaying;
		set
		{
			if ( IsEditorScene ) _isPlaying = value;
			else Player.IsPlaying = value;
		}
	}

	public bool IsLooping
	{
		get => IsEditorScene ? _isLooping : Player.IsLooping;
		set
		{
			if ( IsEditorScene ) _isLooping = Cookies.IsLooping = value;
			else Player.IsLooping = value;
		}
	}

	public float TimeScale
	{
		get => IsEditorScene ? _timeScale : Player.TimeScale;
		set
		{
			if ( IsEditorScene ) _timeScale = Cookies.TimeScale = value;
			else Player.TimeScale = value;
		}
	}

	internal float ScrubbingTimeScale => IsPlaying ? TimeScale : _scrubbingTimeScale;

	public void ApplyFrame( MovieTime time )
	{
		_applyNextFrame = false;

		if ( EditMode is null )
		{
			Player.ApplyFrame( time );
		}
		else
		{
			EditMode?.ApplyFrame( time );
		}
	}

	public void RefreshNextFrame()
	{
		_applyNextFrame = true;
	}

	private void PlaybackFrame( MovieClip clip )
	{
		_scrubbingTimeScale = UpdateScrubbingTimeScale();

		if ( IsPlaying && IsEditorScene )
		{
			var targetTime = CurrentPointer + MovieTime.FromSeconds( RealTime.Delta * TimeScale );

			// got to the end
			if ( targetTime >= clip.Duration && clip.Duration.IsPositive )
			{
				if ( IsLooping )
				{
					targetTime = MovieTime.Zero;
				}
				else
				{
					targetTime = clip.Duration;

					IsPlaying = false;
				}
			}

			SetCurrentPointer( targetTime );
		}
		else if ( _lastPlayerPosition is { } lastPlayerPosition && lastPlayerPosition != Player.Position )
		{
			CurrentPointer = lastPlayerPosition;
			PointerChanged?.Invoke( CurrentPointer );
		}

		_lastPlayerPosition = Player.Position;
	}

	private float UpdateScrubbingTimeScale()
	{
		// While scrubbing, find average scrub speed

		while ( _scrubHistory.TryPeek( out var item ) && item.TimeSince > ScrubHistoryWindow )
		{
			_scrubHistory.Dequeue();

			if ( !IsPlaying ) RefreshNextFrame();
		}

		var time = PreviewPointer ?? CurrentPointer;

		_scrubHistory.Enqueue( (0f, time) );

		var first = _scrubHistory.Peek();

		return time != first.MovieTime
			? Math.Min( MathF.Abs( (float)(time - first.MovieTime).TotalSeconds / ScrubHistoryWindow ), 4f )
			: 0f;
	}
}
