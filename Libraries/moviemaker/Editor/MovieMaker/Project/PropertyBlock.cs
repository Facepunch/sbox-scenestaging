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

	IEnumerable<MovieTime> GetPaintHintTimes( MovieTimeRange timeRange );
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

public sealed record PropertyBlock<T>( PropertySignal<T> Signal, MovieTimeRange TimeRange )
	: IPropertyBlock<T>, IProjectPropertyBlock
{
	public T GetValue( MovieTime time ) => Signal.GetValue( time.Clamp( TimeRange ) );

	public IEnumerable<MovieTime> GetPaintHintTimes( MovieTimeRange timeRange ) =>
		timeRange.Clamp( TimeRange ).GetSampleTimes( 30 );

	public PropertyBlock<T>? Slice( MovieTimeRange timeRange )
	{
		if ( timeRange == TimeRange ) return this;

		if ( timeRange.Intersect( TimeRange ) is not { } intersection )
		{
			return null;
		}

		return intersection.IsEmpty
			? new PropertyBlock<T>( Signal.GetValue( intersection.Start ), intersection )
			: this with { TimeRange = intersection };
	}

	IProjectPropertyBlock? IProjectPropertyBlock.Slice( MovieTimeRange timeRange ) => Slice( timeRange );

	public PropertyBlock<T> Shift( MovieTime offset )
	{
		return !offset.IsZero
			? new PropertyBlock<T>( Signal.Shift( offset ), TimeRange + offset )
			: this;
	}

	IProjectPropertyBlock IProjectPropertyBlock.Shift( MovieTime offset ) => Shift( offset );

	public CompiledPropertyBlock<T> Compile( ProjectPropertyTrack<T> track )
	{
		throw new NotImplementedException();
	}
}
