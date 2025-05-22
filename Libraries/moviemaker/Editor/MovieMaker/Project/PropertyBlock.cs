using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// A <see cref="ITrackBlock"/> that has hints for UI painting.
/// </summary>
public interface IPaintHintBlock : ITrackBlock
{
	/// <summary>
	/// Gets time regions, within <paramref name="timeRange"/>, that have constantly changing values.
	/// </summary>
	IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange );
}

/// <summary>
/// A <see cref="ITrackBlock"/> that can change dynamically, usually for previewing edits / live recordings.
/// </summary>
public interface IDynamicBlock : ITrackBlock
{
	event Action? Changed;
}

/// <summary>
/// A <see cref="IPropertyBlock"/> that can be added to a <see cref="IProjectPropertyTrack"/>.
/// </summary>
public interface IProjectPropertyBlock : IPropertyBlock, IPaintHintBlock
{
	IProjectPropertyBlock? Slice( MovieTimeRange timeRange );
	IProjectPropertyBlock Shift( MovieTime offset );

	PropertySignal Signal { get; }
}

public sealed partial record PropertyBlock<T>( [property: JsonPropertyOrder( 100 )] PropertySignal<T> Signal, MovieTimeRange TimeRange )
	: IPropertyBlock<T>, IProjectPropertyBlock
{
	public T GetValue( MovieTime time ) => Signal.GetValue( time.Clamp( TimeRange ) );

	public IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange ) =>
		Signal.GetPaintHints( timeRange.Clamp( TimeRange ) );

	public PropertyBlock<T>? Slice( MovieTimeRange timeRange )
	{
		if ( timeRange == TimeRange ) return this;

		if ( timeRange.Intersect( TimeRange ) is not { } intersection )
		{
			return null;
		}

		return new PropertyBlock<T>( Signal.Reduce( intersection ), intersection );
	}

	IProjectPropertyBlock? IProjectPropertyBlock.Slice( MovieTimeRange timeRange ) => Slice( timeRange );
	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => new MovieTransform( offset ) * this;
	PropertySignal IProjectPropertyBlock.Signal => Signal;

	public ICompiledPropertyBlock<T> Compile( ProjectPropertyTrack<T> track )
	{
		var sampleRate = track.Project.SampleRate;
		var samples = Signal.Sample( TimeRange, sampleRate );
		var comparer = EqualityComparer<T>.Default;

		if ( samples.All( x => comparer.Equals( x, samples[0] ) ) )
		{
			return new CompiledConstantBlock<T>( TimeRange, samples[0] );
		}

		return new CompiledSampleBlock<T>( TimeRange, 0d, sampleRate, [..samples] );
	}
}
