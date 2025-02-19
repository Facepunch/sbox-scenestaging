using System;

namespace Sandbox.MovieMaker;

#nullable enable

[Icon( "movie" )]
public sealed partial class MoviePlayer : Component
{
	private MovieClip? _embeddedClip;
	private MovieFile? _referencedClip;

	private MovieTime _position;

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
			if ( IsLooping && !duration.IsNegative )
			{
				_position -= duration;
			}
			else
			{
				_position = duration;
				IsPlaying = false;
			}
		}

		ApplyFrame( _position );
	}

	public void ApplyFrame( MovieTime time )
	{
		if ( MovieClip is null || _sceneRefMap.Count == 0 ) return;

		time = time.Clamp( (MovieTime.Zero, MovieClip.Duration) );

		using var sceneScope = Scene.Push();

		foreach ( var track in MovieClip.RootTracks )
		{
			ApplyFrame( track, time );
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
		if ( IsPlaying )
		{
			Position += MovieTime.FromSeconds( Time.Delta * TimeScale );
		}
	}

	private interface IRawRecording
	{
		void Record( MovieTime time );
		void WriteBlocks( MovieTime startTime, int sampleRate );
	}

	private class RawRecording<T> : IRawRecording
	{
		public MovieTrack Track { get; }
		public IMovieProperty<T> Property { get; }
		public List<(MovieTime Time, T Value)> Samples { get; } = new();

		public RawRecording( MovieTrack track, IMovieProperty<T> property )
		{
			Track = track;
			Property = property;
		}

		public void Record( MovieTime time )
		{
			Samples.Add( (time, Property.Value) );
		}

		public void WriteBlocks( MovieTime startTime, int sampleRate )
		{
			if ( Samples.Count == 0 ) return;

			var first = Samples[0].Value;
			var comparer = EqualityComparer<T>.Default;

			if ( Samples.All( x => comparer.Equals( first, x.Value ) ) )
			{
				// Nothing to write
				return;
			}

			// TODO: don't assume fixed sample rate

			var data = new SamplesData<T>( 50, SampleInterpolationMode.Linear,
					Samples.Select( x => x.Value ).ToArray() )
				.Resample( sampleRate );

			Track.AddBlock( new MovieTimeRange( startTime, startTime + data.Duration ), data );
		}
	}

	private readonly Dictionary<MovieTrack, IRawRecording> _recordings = new();
	private static TypeDescription? _rawRecordingType;

	private TypeDescription RawRecordingType => _rawRecordingType ??= TypeLibrary.GetType( typeof( RawRecording<> ) );

	[Property, Button( Icon = "radio_button_checked" ), ShowIf( nameof(IsRecording), false )]
	public void StartRecording()
	{
		if ( MovieClip is not { } clip ) return;

		_recordings.Clear();

		foreach ( var track in clip.RootTracks )
		{
			StartRecording( track );
		}

		IsRecording = true;
		_recordingStartTime = Time.Now;
	}

	private bool StartRecording( MovieTrack track )
	{
		if ( track.EditorData?["Locked"]?.GetValue<bool>() is true ) return false;

		if ( track.Children is { Count: > 0 } children )
		{
			// Don't record this track if any children are recorded instead

			var childRecording = false;

			foreach ( var childTrack in children )
			{
				childRecording |= StartRecording( childTrack );
			}

			if ( childRecording )
			{
				return true;
			}
		}

		if ( GetProperty( track ) is not { IsBound: true } property ) return false;
		if ( property is ISceneReferenceMovieProperty ) return false;

		_recordings.Add( track, RawRecordingType.CreateGeneric<IRawRecording>( [track.PropertyType], [track, property] ) );

		return true;
	}

	[Property, Button( Icon = "stop_circle" ), ShowIf( nameof( IsRecording ), true )]
	public void StopRecording()
	{
		if ( !IsRecording || MovieClip is not { } clip ) return;

		IsRecording = false;

		Log.Info( $"Finished recording {_recordings.Count} tracks!" );

		foreach ( var recording in _recordings.Values )
		{
			recording.WriteBlocks( MovieTime.Zero, clip.DefaultSampleRate );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsRecording )
		{
			return;
		}

		var time = MovieTime.FromSeconds( Time.Now - _recordingStartTime );

		if ( time < MovieTime.Zero ) return;

		foreach ( var recording in _recordings.Values )
		{
			recording.Record( time );
		}
	}
}
