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
	private MovieTime _lastAppliedTime;

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

	public void ApplyFrame( MovieTime time )
	{
		_applyNextFrame = false;

		Parent?.ApplyFrame( SequenceTransform * time );

		foreach ( var view in TrackList.AllTracks )
		{
			view.ApplyFrame( time );
		}

		AdvanceAnimations( time - _lastAppliedTime );

		_lastAppliedTime = time;
	}

	public void RefreshNextFrame()
	{
		_applyNextFrame = true;
	}

	private void PlaybackFrame()
	{
		if ( IsPlaying && IsEditorScene )
		{
			var targetTime = CurrentPointer + MovieTime.FromSeconds( RealTime.Delta * TimeScale );

			// got to the end
			if ( !IsRecording && targetTime >= Project.Duration && Project.Duration.IsPositive )
			{
				if ( IsLooping )
				{
					targetTime = MovieTime.Zero;
				}
				else
				{
					targetTime = Project.Duration;

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
}
