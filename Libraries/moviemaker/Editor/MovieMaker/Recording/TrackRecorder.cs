using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using Editor.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker;

#nullable enable

public record RecorderOptions( int SampleRate )
{
	public static RecorderOptions Default { get; } = new( 30 );
}

public interface ITrackRecorder
{
	IPropertyTrack Track { get; }
	ITrackProperty Target { get; }

	IReadOnlyList<ICompiledPropertyBlock> FinishedBlocks { get; }
	IPropertyBlock? CurrentBlock { get; }

	bool Update( MovieTime deltaTime );
	IReadOnlyList<ICompiledPropertyBlock> ToBlocks();
}

public sealed class TrackRecorderCollection : IReadOnlyList<ITrackRecorder>
{
	private readonly MovieClipRecorder _clipRecorder;
	private readonly List<ITrackRecorder> _recorders = new();

	public int Count => _recorders.Count;

	public ITrackRecorder this[int index] => _recorders[index];

	internal TrackRecorderCollection( MovieClipRecorder clipRecorder )
	{
		_clipRecorder = clipRecorder;
	}

	public void Add( IPropertyTrack track )
	{
		var property = _clipRecorder.Binder.Get( track );
		var recorderType = typeof( TrackRecorder<> ).MakeGenericType( track.TargetType );

		_recorders.Add( (ITrackRecorder)Activator.CreateInstance( recorderType, track, property, _clipRecorder.Options, _clipRecorder.Duration )! );
	}

	public IEnumerator<ITrackRecorder> GetEnumerator() => _recorders.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class MovieClipRecorder
{
	public TrackBinder Binder { get; }
	public RecorderOptions Options { get; }

	public TrackRecorderCollection Tracks { get; }

	public MovieTime Duration { get; private set; }

	public MovieClipRecorder( Scene scene, RecorderOptions? options = null )
		: this( new TrackBinder( scene ), options )
	{

	}

	public MovieClipRecorder( TrackBinder binder, RecorderOptions? options = null )
	{
		Tracks = new( this );

		Binder = binder;
		Options = options ?? RecorderOptions.Default;
	}

	public bool Update( MovieTime deltaTime )
	{
		var anySamplesWritten = false;

		foreach ( var recorder in Tracks )
		{
			anySamplesWritten |= recorder.Update( deltaTime );
		}

		Duration += deltaTime;

		return anySamplesWritten;
	}

	public MovieClip ToClip()
	{
		var compiledDict = new Dictionary<ITrackTarget, ICompiledTrack>();

		foreach ( var recorder in Tracks )
		{
			GetOrCreateCompiledTrack( compiledDict, recorder.Target );
		}

		return MovieClip.FromTracks( compiledDict.Values );
	}

	private ICompiledTrack GetOrCreateCompiledTrack( Dictionary<ITrackTarget, ICompiledTrack> dict, ITrackTarget target )
	{
		if ( dict.TryGetValue( target, out var compiled ) ) return compiled;

		var compiledParent = target.Parent is { } parent ? GetOrCreateCompiledTrack( dict, parent ) : null;

		return dict[target] = target switch
		{
			ITrackReference reference => CreateCompiledReferenceTrack( reference, compiledParent ),
			ITrackProperty property => CreateCompiledPropertyTrack( property, compiledParent ),
			_ => throw new NotImplementedException()
		};
	}

	private ICompiledReferenceTrack CreateCompiledReferenceTrack( ITrackReference target, ICompiledTrack? compiledParent )
	{
		var gameObjectParent = compiledParent as CompiledReferenceTrack<GameObject>;

		return target switch
		{
			ITrackReference<GameObject> => new CompiledReferenceTrack<GameObject>( target.Id, target.Name, gameObjectParent ),
			_ => gameObjectParent?.Component( target.TargetType, target.Id ) ?? MovieClip.RootComponent( target.TargetType, target.Id )
		};
	}

	private ICompiledPropertyTrack CreateCompiledPropertyTrack( ITrackProperty target, ICompiledTrack? compiledParent )
	{
		ArgumentNullException.ThrowIfNull( compiledParent, nameof(compiledParent) );

		var recorder = Tracks.FirstOrDefault( x => x.Target == target );

		return compiledParent.Property( target.Name, target.TargetType, recorder?.ToBlocks() );
	}
}

public sealed class TrackRecorder<T> : ITrackRecorder
{
	private readonly int _sampleRate;

	private readonly List<ICompiledPropertyBlock<T>> _blocks = new();
	private readonly BlockWriter<T> _writer;

	private MovieTime _elapsed;

	private int _sampleCount;

	public IPropertyTrack<T> Track { get; }
	public ITrackProperty<T> Target { get; }

	public IReadOnlyList<ICompiledPropertyBlock<T>> FinishedBlocks => _blocks;
	public IPropertyBlock<T>? CurrentBlock => _writer.IsEmpty ? null : _writer;

	public TrackRecorder( IPropertyTrack<T> track, ITrackProperty<T> target, RecorderOptions options, MovieTime startTime = default )
	{
		Track = track;
		Target = target;

		_sampleRate = options.SampleRate;

		_elapsed = startTime;
		_sampleCount = _elapsed.GetFrameIndex( options.SampleRate );
		_writer = new BlockWriter<T>( options.SampleRate );

		RecordSample();
	}

	public bool Update( MovieTime deltaTime )
	{
		_elapsed += deltaTime;

		var nextCount = _elapsed.GetFrameCount( _sampleRate );
		var anySamplesWritten = false;

		while ( _sampleCount < nextCount )
		{
			RecordSample();
			++_sampleCount;

			anySamplesWritten = true;
		}

		return anySamplesWritten;
	}

	private void RecordSample()
	{
		if ( Target.IsActive )
		{
			if ( _writer.IsEmpty )
			{
				_writer.StartTime = MovieTime.FromFrames( _sampleCount, _sampleRate );
			}

			_writer.Write( Target.Value );
		}
		else
		{
			FinishBlock();
		}
	}

	private void FinishBlock()
	{
		if ( _writer.IsEmpty ) return;

		_blocks.Add( _writer.Compile() );

		_writer.Clear();
	}

	public ImmutableArray<ICompiledPropertyBlock<T>> ToBlocks()
	{
		FinishBlock();

		return [.._blocks];
	}

	IPropertyTrack ITrackRecorder.Track => Track;
	ITrackProperty ITrackRecorder.Target => Target;
	IReadOnlyList<ICompiledPropertyBlock> ITrackRecorder.FinishedBlocks => FinishedBlocks;
	IPropertyBlock? ITrackRecorder.CurrentBlock => CurrentBlock;
	IReadOnlyList<ICompiledPropertyBlock> ITrackRecorder.ToBlocks() => ToBlocks();
}

internal class BlockWriter<T>( int sampleRate ) : IPropertyBlock<T>, IDynamicBlock, IPaintHintBlock
{
	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();

	private readonly List<T> _samples = new();
	private T _defaultValue;

	public bool IsEmpty => _samples.Count == 0;

	public event Action? Changed;

	public MovieTime StartTime { get; set; }

	public MovieTimeRange TimeRange => (StartTime, StartTime + MovieTime.FromFrames( _samples.Count, sampleRate ));

	public void Clear()
	{
		_samples.Clear();
	}

	public void Write( T value )
	{
		_samples.Add( value );
		_defaultValue = value;

		Changed?.Invoke();
	}

	public ICompiledPropertyBlock<T> Compile()
	{
		if ( IsEmpty ) throw new InvalidOperationException( "Block is empty!" );

		return new CompiledSampleBlock<T>( TimeRange, 0d, sampleRate, [.._samples] );
	}

	public IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange ) => [TimeRange];

	public T GetValue( MovieTime time )
	{
		return _samples.Count != 0
			? _samples.Sample( time - StartTime, sampleRate, _interpolator )
			: _defaultValue;
	}
}
