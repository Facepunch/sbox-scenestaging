using System;

namespace Sandbox.MovieMaker;

#nullable enable

[Icon( "movie" )]
public sealed partial class MoviePlayer : Component
{
	private MovieClip? _embeddedClip;
	private MovieFile? _referencedClip;

	private float _position;

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
	public bool IsPlaying { get; set; }

	[Property, Group( "Playback" )]
	public bool IsLooping { get; set; }

	[Property, Group( "Playback" )]
	public float TimeScale { get; set; } = 1f;

	[Property, Group( "Playback" )]
	public float Position
	{
		get => _position;
		set
		{
			_position = value;
			UpdatePosition();
		}
	}

	[Property, Group( "Recording" )]
	public bool IsRecording { get; private set; }

	private float _recordingStartTime;

	/// <summary>
	/// Apply the movie clip to the scene at the current time position.
	/// If we reach the end, check <see cref="IsLooping"/> to either jump back to the start, or stop playback.
	/// </summary>
	private void UpdatePosition()
	{
		if ( MovieClip is null ) return;

		var duration = MovieClip.Duration;

		if ( _position >= duration )
		{
			if ( IsLooping && duration > 0f )
			{
				_position -= MathF.Floor( _position / duration ) * duration;
			}
			else
			{
				_position = duration;
				IsPlaying = false;
			}
		}

		ApplyFrame( _position );
	}

	public void ApplyFrame( float time )
	{
		if ( MovieClip is null || _sceneRefMap.Count == 0 ) return;

		time = Math.Clamp( time, 0f, MovieClip.Duration );

		using var sceneScope = Scene.Push();

		foreach ( var track in MovieClip.RootTracks )
		{
			ApplyFrame( track, time );
		}
	}

	internal void ApplyFrame( MovieTrack track, float time )
	{
		// TODO: this is a slow placeholder implementation, we can avoid boxing / reflection when we're in the engine

		if ( GetOrAutoResolveProperty( track ) is { IsBound: true } property && track.GetBlock( time ) is { } block )
		{
			switch ( block.Data )
			{
				case IConstantData constantData:
					property.Value = constantData.Value;
					break;

				case ISamplesData samplesData:
					property.Value = samplesData.GetValue( time - block.StartTime );
					break;

				case ActionData:
					throw new NotImplementedException();
			}
		}

		foreach ( var child in track.Children )
		{
			ApplyFrame( child, time );
		}
	}

	protected override void OnEnabled()
	{
		UpdatePosition();
	}

	protected override void OnUpdate()
	{
		if ( IsPlaying )
		{
			Position += Time.Delta * TimeScale;
		}
	}

	private interface IRawRecording
	{
		void Record( float time );
		void WriteBlocks();
	}

	private class RawRecording<T> : IRawRecording
	{
		public MovieTrack Track { get; }
		public IMovieProperty<T> Property { get; }
		public List<(float Time, T Value)> Samples { get; } = new List<(float Time, T Value)>();

		public RawRecording( MovieTrack track, IMovieProperty<T> property )
		{
			Track = track;
			Property = property;
		}

		public void Record( float time )
		{
			Samples.Add( (time, Property.Value) );
		}

		public void WriteBlocks()
		{
			// TODO: resample

			var data = new SamplesData<T>( 50f, SampleInterpolationMode.Linear,
				Samples.Select( x => x.Value ).ToArray() );

			Track.AddBlock( 0f, Samples.Count / 50f, data );
		}
	}

	private readonly Dictionary<MovieTrack, IRawRecording> _recordings = new();

	[Property, Button( Icon = "radio_button_checked" ), ShowIf( nameof(IsRecording), false )]
	public void StartRecording()
	{
		if ( MovieClip is not { } clip ) return;

		_recordings.Clear();

		var rawRecordingType = TypeLibrary.GetType( typeof(RawRecording<>) );

		foreach ( var track in clip.AllTracks )
		{
			if ( GetProperty( track ) is not { IsBound: true } property )
			{
				continue;
			}

			if ( property is ISceneReferenceMovieProperty )
			{
				continue;
			}

			_recordings.Add( track, rawRecordingType.CreateGeneric<IRawRecording>( [track.PropertyType], [track, property] ) );
		}

		IsRecording = true;
		_recordingStartTime = Time.Now;
	}

	[Property, Button( Icon = "stop_circle" ), ShowIf( nameof( IsRecording ), true )]
	public void StopRecording()
	{
		if ( !IsRecording ) return;

		IsRecording = false;

		Log.Info( $"Finished recording {_recordings.Count} tracks!" );

		foreach ( var recording in _recordings.Values )
		{
			recording.WriteBlocks();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsRecording )
		{
			return;
		}

		var time = Time.Now - _recordingStartTime;

		if ( time < 0f ) return;

		foreach ( var recording in _recordings.Values )
		{
			recording.Record( time );
		}
	}
}
