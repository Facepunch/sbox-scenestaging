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

		foreach ( var track in Session.EditableTracks )
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

		var tracks = new Dictionary<IProjectTrack, ICompiledTrack>();
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

		var sourceClip = Project.AddSourceClip( MovieClip.FromTracks( tracks.Values ), new JsonObject
		{
			{ "Date", DateTime.UtcNow.ToString( "o", CultureInfo.InvariantCulture ) },
			{ "IsEditor", Session.Player.Scene.IsEditor },
			{ "SceneSource", Json.ToNode( Session.Player.Scene.Source ) },
			{ "MoviePlayer", Json.ToNode( Session.Player.Id ) }
		} );

		Clipboard = new ClipboardData( new TimeSelection( range, DefaultInterpolation ), tracks.Keys
			.OfType<IProjectPropertyTrack>()
			.ToImmutableDictionary( x => x.Id, x => x.CreateSourceBlocks( sourceClip ) ) );

		Session.SetCurrentPointer( range.Start );

		if ( LoadChangesFromClipboard() )
		{
			DisplayAction( "radio_button_checked" );
		}
	}

	private static ICompiledTrack GetOrCreateCompiledTrack( Dictionary<IProjectTrack, ICompiledTrack> dict, IProjectTrack track )
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
	ICompiledPropertyTrack? Compile( IProjectPropertyTrack track, ICompiledTrack compiledParent );
}

file class TrackRecording<T> : ITrackRecording, IPropertyBlock<T>
{
	/// <summary>
	/// Use a <see cref="CompiledConstantBlock{T}"/> if we see at least this many identical samples in a row.
	/// </summary>
	private const int ConstantBlockMinimumSampleCount = 15;

	public ProjectPropertyTrack<T> Track { get; }
	public ITrackProperty<T> Property { get; }
	public int SampleRate { get; }
	public MovieTime SampleInterval { get; }

	public MovieTimeRange TimeRange => (_startTime, _startTime + MovieTime.FromFrames( _currentBlockSamples.Count, SampleRate ));

	private readonly MovieTime _startTime;
	private readonly IInterpolator<T>? _interpolator;

	private readonly CurrentBlock _currentBlock;

	private class CurrentBlock
	{
		private static readonly EqualityComparer<T> _comparer = EqualityComparer<T>.Default;

		private readonly int _sampleRate;
		private readonly IInterpolator<T>? _interpolator;

		private readonly List<T> _samples = new();
		private int _startIndex;
		private int _totalCount;
		private int _lastSampleRepeats;

		public MovieTimeRange TimeRange => (MovieTime.FromFrames( _startIndex, _sampleRate ), MovieTime.FromFrames( _totalCount, _sampleRate ));

		public CurrentBlock( int sampleRate, IInterpolator<T>? interpolator )
		{
			_sampleRate = sampleRate;
			_interpolator = interpolator;

			_startIndex = 0;
			_totalCount = 0;
		}

		public T GetValue( MovieTime time ) => _samples.Sample( time - TimeRange.Start, _sampleRate, _interpolator );

		public ICompiledPropertyBlock<T>? AddSample( T value )
		{
			if ( _samples.Count > 0 )
			{
				if ( _comparer.Equals( _samples[^1], value ) )
				{
					_lastSampleRepeats++;
					_totalCount++;
					return null;
				}

				var last = _samples[^1];

				for ( var i = 0; i < _lastSampleRepeats; ++i )
				{
					_samples.Add( last );
				}
			}

			_samples.Add( value );
			_lastSampleRepeats = 0;
			_startIndex = _totalCount;
			_totalCount++;

			return null;
		}

		public ICompiledPropertyBlock<T>? FinishBlock()
		{
			if ( _samples.Count == 0 ) return null;

			if ( _samples.Count == 1 )
			{
				return new CompiledConstantBlock<T>( TimeRange, _samples[0] );
			}


		}
	}

	private readonly List<ICompiledPropertyBlock<T>> _finishedBlocks = new();

	public event Action? Changed;

	public TrackRecording( ProjectPropertyTrack<T> track, ITrackProperty<T> property, MovieTime startTime )
	{
		Track = track;
		Property = property;

		SampleRate = track.Project.SampleRate;
		SampleInterval = MovieTime.FromFrames( 1, SampleRate );

		_interpolator = Interpolator.GetDefault<T>();

		_startTime = startTime.SnapToGrid( SampleInterval );
		_currentBlock = new CurrentBlock( SampleRate, _interpolator );
	}

	public void Record( MovieTime time )
	{
		var index = (time - _startTime).GetFrameIndex( SampleRate );

		if ( index < 0 ) return;

		if ( index < _currentBlockSamples.Count )
		{
			_currentBlockSamples.RemoveRange( index, _currentBlockSamples.Count - index );
		}

		AddSample( Property.Value, index - _currentBlockSamples.Count + 1 );
	}

	private void AddSample( T value, int offset )
	{
		if ( offset > 1 )
		{
			var prev = _lastValue;

			for ( var i = 1; i < offset; ++i )
			{
				AddSampleCore( _interpolator is not null
					? _interpolator.Interpolate( prev, value, (float)i / offset )
					: prev );
			}
		}

		AddSampleCore( value );
		Changed?.Invoke();
	}

	private void AddSampleCore( T value )
	{
		++_totalSampleCount;

		if ( _comparer.Equals( _lastValue, value ) ) return;

		var identicalSampleCount = _totalSampleCount - _lastChangeIndex;

		if ( identicalSampleCount >= ConstantBlockMinimumSampleCount )
		{
			FinishSampleBlock();

			var startTime = MovieTime.FromFrames( _lastChangeIndex, SampleRate );
			var endTime = MovieTime.FromFrames( _totalSampleCount, SampleRate );

			_finishedBlocks.Add( new CompiledConstantBlock<T>( (startTime, endTime), _lastValue ) );
		}
		else
		{
			for ( var i = 0; i < identicalSampleCount; ++i )
			{
				_currentBlockSamples.Add( _lastValue );
			}
		}

		_currentBlockIndex = _totalSampleCount;
		_lastChangeIndex = _totalSampleCount;
		_lastValue = value;
	}

	private void FinishBlock()
	{
		var identicalSampleCount = _totalSampleCount - _lastChangeIndex;

		for ( var i = 0; i < identicalSampleCount; ++i )
		{
			_currentBlockSamples.Add( _lastValue );
		}

		FinishSampleBlock();
	}

	private void FinishSampleBlock()
	{
		if ( _currentBlockSamples.Count == 0 ) return;

		var startTime = MovieTime.FromFrames( _lastChangeIndex - _currentBlockSamples.Count, SampleRate );
		var endTime = MovieTime.FromFrames( _lastChangeIndex, SampleRate );

		_finishedBlocks.Add( new CompiledSampleBlock<T>( (startTime, endTime), 0d, SampleRate, [.._currentBlockSamples] ) );
		_currentBlockSamples.Clear();
	}

	public ICompiledPropertyTrack? Compile( IProjectPropertyTrack track, ICompiledTrack compiledParent )
	{
		FinishBlock();

		return new CompiledPropertyTrack<T>( track.Name, compiledParent, [.._finishedBlocks] );
	}

	public T GetValue( MovieTime time )
	{
		var index = (time - _startTime).GetFrameIndex( SampleRate );

		if ( index >= _lastChangeIndex ) return _lastValue;


		return _currentBlockSamples.Sample( time - TimeRange.Start, SampleRate, _interpolator );
	}

	public IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange ) => [TimeRange];
}
