using System.Diagnostics.CodeAnalysis;
using Sandbox.MovieMaker.Compiled;

namespace Sandbox.MovieMaker;

#nullable enable

public record RecorderOptions( int SampleRate )
{
	public static RecorderOptions Default { get; } = new( 30 );
}

public interface IPropertyRecorder
{
	void Update( MovieTime deltaTime );
	ICompiledPropertyTrack Compile();
}

public interface IPropertyRecorder<T> : IPropertyRecorder, IPropertyTrack<T>
{
	new CompiledPropertyTrack<T> Compile();

	ICompiledPropertyTrack IPropertyRecorder.Compile() => Compile();
}

public interface IClipRecorder
{
	void Update( MovieTime deltaTime );
	MovieClip Compile();
}

partial interface ITrackProperty
{
	IPropertyRecorder CreateRecorder( RecorderOptions? options = null );
}

partial interface ITrackProperty<T>
{
	new IPropertyRecorder<T> CreateRecorder( RecorderOptions? options = null ) =>
		new PropertyRecorder<T>( this, options ?? RecorderOptions.Default );

	IPropertyRecorder ITrackProperty.CreateRecorder( RecorderOptions? options ) => CreateRecorder( options );
}

public static class RecorderExtensions
{
	public static IClipRecorder CreateRecorder( this IClip clip, RecorderOptions? options = null, TrackBinder? binder = null )
	{
		binder ??= TrackBinder.Default;
		options ??= RecorderOptions.Default;

		var properties = clip.Tracks
			.OfType<IPropertyTrack>()
			.Select( binder.Get )
			.Distinct();

		return new ClipRecorder( properties, options );
	}

	public static IPropertyRecorder CreateRecorder( this IPropertyTrack track, RecorderOptions? options = null, TrackBinder? binder = null )
	{
		binder ??= TrackBinder.Default;

		return binder.Get( track ).CreateRecorder( options );
	}

	public static IPropertyRecorder<T> CreateRecorder<T>( this IPropertyTrack<T> track, RecorderOptions? options = null, TrackBinder? binder = null )
	{
		binder ??= TrackBinder.Default;

		return binder.Get( track ).CreateRecorder( options );
	}
}

file sealed class ClipRecorder : IClipRecorder
{
	private readonly List<IPropertyRecorder> _recorders;

	public ClipRecorder( IEnumerable<ITrackProperty> properties, RecorderOptions options )
	{
		_recorders = properties
			.Distinct()
			.Select( x => x.CreateRecorder( options ) )
			.ToList();
	}

	public void Update( MovieTime deltaTime )
	{
		foreach ( var recorder in _recorders )
		{
			recorder.Update( deltaTime );
		}
	}

	public MovieClip Compile()
	{
		throw new System.NotImplementedException();
	}
}

file sealed class PropertyRecorder<T> : IPropertyRecorder<T>
{
	private readonly ITrackProperty<T> _property;
	private readonly int _sampleRate;

	private readonly List<T> _samples = new();

	private MovieTime _elapsed;
	private int _sampleCount;

	public PropertyRecorder( ITrackProperty<T> property, RecorderOptions options )
	{
		_property = property;
		_sampleRate = options.SampleRate;

		_elapsed = 0d;

		RecordSample();
	}

	public void Update( MovieTime deltaTime )
	{
		_elapsed += deltaTime;

		var nextCount = _elapsed.GetFrameIndex( _sampleRate );

		while ( _sampleCount < nextCount )
		{
			RecordSample();
		}
	}

	private void RecordSample()
	{

	}

	public CompiledPropertyTrack<T> Compile()
	{
		throw new System.NotImplementedException();
	}

	public string Name => _track.Name;

	public ITrack Parent => _track.Parent;

	public bool TryGetValue( MovieTime time, [MaybeNullWhen( false )] out T value )
	{
		throw new System.NotImplementedException();
	}
}
