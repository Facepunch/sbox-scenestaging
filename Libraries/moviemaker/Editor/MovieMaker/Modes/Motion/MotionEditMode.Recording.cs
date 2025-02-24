using Sandbox.MovieMaker;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

partial class MotionEditMode
{
	private bool _stopPlayingAfterRecording;

	public override bool AllowRecording => true;

	private readonly Dictionary<IProjectPropertyTrack, ITrackRecording> _recordings = new();

	protected override bool OnStartRecording()
	{
		_recordings.Clear();

		var time = Session.CurrentPointer;

		foreach ( var track in Project.RootTracks )
		{
			StartRecording( track, time );
		}

		if ( _recordings.Count == 0 ) return false;

		_stopPlayingAfterRecording = !Session.IsPlaying;
		Session.IsPlaying = true;

		return true;
	}

	private bool StartRecording( IProjectTrack track, MovieTime time )
	{
		if ( track is not IProjectPropertyTrack propertyTrack ) return false;
		if ( !Session.CanEdit( track ) ) return false;

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

		if ( Session.Binder.Get( propertyTrack ) is not { IsBound: true, CanWrite: true } property ) return false;

		var recordingType = typeof(TrackRecording<>).MakeGenericType( track.TargetType );
		var recording = (ITrackRecording)Activator.CreateInstance( recordingType, track, property, time )!;

		_recordings.Add( propertyTrack, recording );

		SetPreviewBlocks( propertyTrack, [recording] );

		return true;
	}

	protected override void OnStopRecording()
	{
		if ( _stopPlayingAfterRecording )
		{
			Session.IsPlaying = false;
		}

		Log.Info( $"Finished recording {_recordings.Count} tracks!" );

		var tracks = new Dictionary<IProjectTrack, CompiledTrack>();
		MovieTimeRange? timeRange = null;

		foreach ( var (track, recording) in _recordings )
		{
			ClearPreviewBlocks( track );

			var compiledParent = GetOrCreateCompiledTrack( tracks, track.Parent! );

			if ( recording.Compile( track, compiledParent ) is not { } compiledTrack )
			{
				continue;
			}

			tracks[track] = compiledTrack;
			timeRange = timeRange?.Union( compiledTrack.TimeRange ) ?? compiledTrack.TimeRange;
		}

		if ( tracks.Count <= 0 || timeRange is not { Duration.IsPositive: true } range ) return;

		var sourceClip = Project.AddSourceClip( new CompiledClip( tracks.Values ), new JsonObject
		{
			{ "Date", DateTime.UtcNow.ToString( "o", CultureInfo.InvariantCulture ) },
			{ "IsEditor", Session.Player.Scene.IsEditor },
			{ "SceneSource", Json.ToNode( Session.Player.Scene.Source ) },
			{ "MoviePlayer", Json.ToNode( Session.Player ) }
		} );

		Clipboard = new ClipboardData( new TimeSelection( range, DefaultInterpolation ), tracks.Keys
			.OfType<IProjectPropertyTrack>()
			.ToImmutableDictionary( x => x.Id, x => x.CreateSourceBlocks( sourceClip ) ) );

		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "radio_button_checked" );
		}
	}

	private static CompiledTrack GetOrCreateCompiledTrack( Dictionary<IProjectTrack, CompiledTrack> dict, IProjectTrack track )
	{
		if ( dict.TryGetValue( track, out var compiled ) ) return compiled;

		var compiledParent = track.Parent is { } parent ? GetOrCreateCompiledTrack( dict, parent ) : null;

		return dict[track] = track switch
		{
			IProjectReferenceTrack refTrack => refTrack.Compile( compiledParent, true ),
			IProjectPropertyTrack propertyTrack => propertyTrack.Compile( compiledParent, true ),
			_ => throw new NotImplementedException()
		};
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

internal interface ITrackRecording : IPropertyBlock, IDynamicBlock
{
	void Record( MovieTime time );
	CompiledPropertyTrack? Compile( IProjectPropertyTrack track, CompiledTrack compiledParent );
}

file class TrackRecording<T> : ITrackRecording, IPropertyBlock<T>
{
	public ProjectPropertyTrack<T> Track { get; }
	public ITrackProperty<T> Property { get; }
	public int SampleRate { get; }
	public MovieTime SampleInterval { get; }

	public MovieTimeRange TimeRange => (_startTime, _startTime + MovieTime.FromFrames( _samples.Count, SampleRate ));

	private readonly MovieTime _startTime;
	private readonly IInterpolator<T>? _interpolator;

	private readonly List<T> _samples = new();

	public event Action? Changed;

	public TrackRecording( ProjectPropertyTrack<T> track, ITrackProperty<T> property, MovieTime startTime )
	{
		Track = track;
		Property = property;

		SampleRate = track.Project.SampleRate;
		SampleInterval = MovieTime.FromFrames( 1, SampleRate );

		_interpolator = Interpolator.GetDefault<T>();

		_startTime = startTime.SnapToGrid( SampleInterval );
		_samples.Add( property.Value );
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

	public CompiledPropertyTrack? Compile( IProjectPropertyTrack track, CompiledTrack compiledParent )
	{
		if ( _samples.Count == 0 ) return null;

		var first = _samples[0];
		var comparer = EqualityComparer<T>.Default;

		if ( _samples.All( x => comparer.Equals( first, x ) ) ) return null;

		var data = new CompiledSampleBlock<T>( TimeRange - _startTime, 0d, SampleRate, [.._samples] );

		return new CompiledPropertyTrack<T>( track.Name, compiledParent, [data] );
	}

	public T GetValue( MovieTime time ) => _samples.Sample( time - TimeRange.Start, SampleRate, _interpolator );

	public IEnumerable<MovieTime> GetPaintHintTimes( MovieTimeRange timeRange )
	{
		return timeRange.GetSampleTimes( _startTime, _samples.Count, SampleRate );
	}
}
