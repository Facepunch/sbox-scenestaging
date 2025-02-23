using Sandbox.MovieMaker;
using System.Collections.Immutable;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private bool _stopPlayingAfterRecording;

	public override bool AllowRecording => true;

	private readonly Dictionary<MovieTrack, ITrackRecording> _recordings = new();

	protected override bool OnStartRecording()
	{
		_recordings.Clear();

		var time = Session.CurrentPointer;

		foreach ( var track in Clip.RootTracks )
		{
			StartRecording( track, time );
		}

		if ( _recordings.Count == 0 ) return false;

		_stopPlayingAfterRecording = !Session.IsPlaying;
		Session.IsPlaying = true;

		return true;
	}

	private bool StartRecording( MovieTrack track, MovieTime time )
	{
		if ( track.ReadEditorData()?.Locked is true ) return false;

		if ( track.Children is { Count: > 0 } children )
		{
			// Don't record this track if any children are recorded instead

			var childRecording = false;

			foreach ( var childTrack in children )
			{
				childRecording |= StartRecording( childTrack, time );
			}

			if ( childRecording )
			{
				return true;
			}
		}

		if ( Session.Player.GetProperty( track ) is not { IsBound: true } property ) return false;
		if ( property is ISceneReferenceMovieProperty ) return false;

		var recordingType = typeof(TrackRecording<>).MakeGenericType( track.PropertyType );
		var recording = (ITrackRecording)Activator.CreateInstance( recordingType, track, property, time )!;

		_recordings.Add( track, recording );

		SetPreviewBlocks( track, [recording] );

		return true;
	}

	protected override void OnStopRecording()
	{
		if ( _stopPlayingAfterRecording )
		{
			Session.IsPlaying = false;
		}

		Log.Info( $"Finished recording {_recordings.Count} tracks!" );

		var tracks = new Dictionary<Guid, IReadOnlyList<MovieBlockSlice>>();

		MovieTimeRange? timeRange = null;

		foreach ( var (track, recording) in _recordings )
		{
			ClearPreviewBlocks( track );

			if ( recording.GetBlocks() is not { Count: > 0 } blocks ) continue;

			tracks.Add( track.Id, blocks );

			foreach ( var block in blocks )
			{
				timeRange = timeRange?.Union( block.TimeRange ) ?? block.TimeRange;
			}
		}

		if ( tracks.Count <= 0 || timeRange is not { } range ) return;

		var offset = Session.CurrentPointer;

		Clipboard = new ClipboardData( new TimeSelection( range - offset, DefaultInterpolation ), tracks.ToImmutableDictionary( x => x.Key,
				x => (IReadOnlyList<MovieBlockSlice>)x.Value.Select( y => y with { TimeRange = y.TimeRange - offset } ).ToImmutableArray() ) );

		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "radio_button_checked" );
		}
	}

	private void RecordingFrame()
	{
		if ( !Session.IsRecording ) return;

		var time = Session.CurrentPointer;

		foreach ( var recording in _recordings.Values )
		{
			recording.Record( time );
		}
	}
}

internal interface ITrackRecording : IPreviewMovieBlock
{
	void Record( MovieTime time );
	IReadOnlyList<MovieBlockSlice> GetBlocks();
}

file class TrackRecording<T> : ITrackRecording
{
	public MovieTrack Track { get; }
	public IMovieProperty<T> Property { get; }
	public int SampleRate { get; }
	public MovieTime SampleInterval { get; }

	public MovieTimeRange TimeRange => (_startTime, _startTime + MovieTime.FromFrames( _samples.Count, SampleRate ));
	IMovieBlockData IMovieBlock.Data => _previewData;

	private readonly MovieTime _startTime;
	private readonly IInterpolator<T>? _interpolator;

	private readonly List<T> _samples = new();
	private readonly SamplesData<T> _previewData;

	public event Action? Changed;

	public TrackRecording( MovieTrack track, IMovieProperty<T> property, MovieTime startTime )
	{
		Track = track;
		Property = property;

		SampleRate = track.Clip.DefaultSampleRate;
		SampleInterval = MovieTime.FromFrames( 1, SampleRate );

		_interpolator = Interpolator.GetDefault<T>();

		_startTime = startTime.SnapToGrid( SampleInterval );
		_samples.Add( property.Value );

		_previewData = new SamplesData<T>( SampleRate, SampleInterpolationMode.Linear, _samples );
	}

	public void Record( MovieTime time )
	{
		var index = (time - _startTime).GetFrameIndex( SampleRate );

		if ( index < 0 ) return;

		if ( index < _samples.Count )
		{
			_samples.RemoveRange( index, _samples.Count - index );
		}

		AddSample( Property.Value, index - _samples.Count + 1 );
	}

	private void AddSample( T value, int offset )
	{
		_samples.EnsureCapacity( _samples.Count + offset );

		if ( offset > 1 )
		{
			var prev = _samples[^1];

			for ( var i = 1; i < offset; ++i )
			{
				_samples.Add( _interpolator is not null
					? _interpolator.Interpolate( prev, value, (float)i / offset )
					: prev );
			}
		}

		_samples.Add( value );

		Changed?.Invoke();
	}

	public IReadOnlyList<MovieBlockSlice> GetBlocks()
	{
		if ( _samples.Count == 0 ) return [];

		var first = _samples[0];
		var comparer = EqualityComparer<T>.Default;

		if ( _samples.All( x => comparer.Equals( first, x ) ) ) return [];

		var data = new SamplesData<T>( SampleRate, SampleInterpolationMode.Linear, _samples.ToArray() );

		return [new MovieBlockSlice( TimeRange, data )];
	}
}
