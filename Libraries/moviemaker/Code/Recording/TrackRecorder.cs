using System;
using System.Collections.Immutable;
using System.Reflection;
using Sandbox.MovieMaker.Compiled;
using static Sandbox.Resources.ResourceGenerator;

namespace Sandbox.MovieMaker;

#nullable enable

public record RecorderOptions( int SampleRate )
{
	public static RecorderOptions Default { get; } = new( 30 );
}

public interface ITrackRecorder
{
	ITrackProperty Target { get; }

	void Update( MovieTime deltaTime );
	IReadOnlyList<ICompiledPropertyBlock> ToBlocks();
}

partial interface ITrackProperty
{
	ITrackRecorder CreateRecorder( RecorderOptions? options = null );
}

partial interface ITrackProperty<T>
{
	new TrackRecorder<T> CreateRecorder( RecorderOptions? options = null ) =>
		new ( this, options ?? RecorderOptions.Default );

	ITrackRecorder ITrackProperty.CreateRecorder( RecorderOptions? options ) => CreateRecorder( options );
}

public static class RecorderExtensions
{
	public static MovieClipRecorder CreateRecorder( this IClip clip,
		RecorderOptions? options = null, TrackBinder? binder = null ) =>
			clip.Tracks.CreateRecorder( options, binder );

	public static MovieClipRecorder CreateRecorder( this IEnumerable<ITrack> tracks,
		RecorderOptions? options = null, TrackBinder? binder = null )
	{
		binder ??= TrackBinder.Default;
		options ??= RecorderOptions.Default;

		var properties = tracks
			.OfType<IPropertyTrack>()
			.Select( binder.Get )
			.Distinct();

		return new MovieClipRecorder( properties, options );
	}

	public static ITrackRecorder CreateRecorder( this IPropertyTrack track,
		RecorderOptions? options = null, TrackBinder? binder = null )
	{
		binder ??= TrackBinder.Default;

		return binder.Get( track ).CreateRecorder( options );
	}

	public static TrackRecorder<T> CreateRecorder<T>( this IPropertyTrack<T> track,
		RecorderOptions? options = null, TrackBinder? binder = null )
	{
		binder ??= TrackBinder.Default;

		return binder.Get( track ).CreateRecorder( options );
	}
}

public sealed class MovieClipRecorder
{
	private readonly ImmutableArray<ITrackRecorder> _recorders;

	public MovieClipRecorder( IEnumerable<ITrack> tracks, TrackBinder? binder = null, RecorderOptions? options = null )
	{
		binder ??= TrackBinder.Default;

		var properties = tracks
			.OfType<IPropertyTrack>()
			.Select( binder.Get );

		_recorders = CreateRecorders( properties, options ?? RecorderOptions.Default );
	}

	public MovieClipRecorder( IEnumerable<ITrackProperty> properties, RecorderOptions? options = null )
	{
		_recorders = CreateRecorders( properties, options ?? RecorderOptions.Default );
	}

	public void Update( MovieTime deltaTime )
	{
		foreach ( var recorder in _recorders )
		{
			recorder.Update( deltaTime );
		}
	}

	public MovieClip ToClip()
	{
		var compiledDict = new Dictionary<ITrackTarget, ICompiledTrack>();

		foreach ( var recorder in _recorders )
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

		var recorder = _recorders.FirstOrDefault( x => x.Target == target );

		return compiledParent.Property( target.Name, target.TargetType, recorder?.ToBlocks() );
	}

	private static ImmutableArray<ITrackRecorder> CreateRecorders( IEnumerable<ITrackProperty> properties, RecorderOptions options )
	{
		return properties
			.Distinct()
			.Select( x => x.CreateRecorder( options ) )
			.ToImmutableArray();
	}
}

public sealed class TrackRecorder<T> : ITrackRecorder
{
	private readonly int _sampleRate;

	private readonly List<T> _currentBlockSamples = new();
	private readonly List<ICompiledPropertyBlock<T>> _blocks = new();

	private MovieTime _elapsed;
	private int _sampleCount;

	public ITrackProperty<T> Target { get; }

	public TrackRecorder( ITrackProperty<T> target, RecorderOptions options )
	{
		Target = target;

		_sampleRate = options.SampleRate;
		_elapsed = 0d;

		RecordSample();
	}

	public void Update( MovieTime deltaTime )
	{
		_elapsed += deltaTime;

		var nextCount = _elapsed.GetFrameCount( _sampleRate );

		while ( _sampleCount < nextCount )
		{
			RecordSample();
		}
	}

	private void RecordSample()
	{
		++_sampleCount;

		if ( Target.IsActive )
		{
			_currentBlockSamples.Add( Target.Value );
		}
		else
		{
			FinishBlock();
		}
	}

	private void FinishBlock()
	{
		if ( _currentBlockSamples.Count == 0 ) return;

		var startIndex = _sampleCount - _currentBlockSamples.Count;
		var endIndex = _sampleCount;

		var startTime = MovieTime.FromFrames( startIndex, _sampleRate );
		var endTime = MovieTime.FromFrames( endIndex, _sampleRate );

		_blocks.Add( new CompiledSampleBlock<T>( (startTime, endTime), 0d, _sampleRate,
			_currentBlockSamples.ToImmutableArray() ) );

		_currentBlockSamples.Clear();
	}

	public ImmutableArray<ICompiledPropertyBlock<T>> ToBlocks()
	{
		FinishBlock();

		return _blocks.ToImmutableArray();
	}

	ITrackProperty ITrackRecorder.Target => Target;
	IReadOnlyList<ICompiledPropertyBlock> ITrackRecorder.ToBlocks() => ToBlocks();
}
