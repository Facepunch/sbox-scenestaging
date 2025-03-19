using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// A <see cref="IPropertySignal"/> with a defined start and end time.
/// </summary>
public interface IPropertyBlock : IPropertySignal
{
	MovieTimeRange TimeRange { get; }

	IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange );
}

/// <summary>
/// A <see cref="IPropertyBlock"/> that can change dynamically, usually for previewing edits / live recordings.
/// </summary>
public interface IDynamicBlock : IPropertyBlock
{
	event Action? Changed;
}

/// <inheritdoc cref="IPropertyBlock"/>
public interface IPropertyBlock<T> : IPropertyBlock, IPropertySignal<T>;

/// <summary>
/// A <see cref="IPropertyBlock"/> that can be added to a <see cref="IProjectPropertyTrack"/>.
/// </summary>
public interface IProjectPropertyBlock : IPropertyBlock
{
	IProjectPropertyBlock? Slice( MovieTimeRange timeRange );
	IProjectPropertyBlock Shift( MovieTime offset );
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

	public PropertyBlock<T> Shift( MovieTime offset )
	{
		return !offset.IsZero
			? new PropertyBlock<T>( Signal.Shift( offset ).Reduce( TimeRange + offset ), TimeRange + offset )
			: this;
	}

	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => Shift( offset );

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
