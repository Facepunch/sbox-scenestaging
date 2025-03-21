using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// A <see cref="IPropertySignal{T}"/> that can be composed with <see cref="PropertyOperation{T}"/>s,
/// and stored in a <see cref="IPropertyBlock{T}"/>.
/// </summary>
public abstract partial record PropertySignal<T> : IPropertySignal<T>
{
	public abstract T GetValue( MovieTime time );

	public PropertySignal<T> Transform( MovieTime offset )
	{
		return !offset.IsZero ? OnTransform( offset ) : this;
	}

	protected abstract PropertySignal<T> OnTransform( MovieTime offset );

	/// <summary>
	/// Try to make a more minimal composition for this signal, optionally within a time range.
	/// </summary>
	/// <param name="start">Optional start time, we can discard any features before this if given.</param>
	/// <param name="end">Optional end time, we can discard any features after this if given.</param>
	public PropertySignal<T> Reduce( MovieTime? start = null, MovieTime? end = null )
	{
		return start >= end ? GetValue( start.Value ) : OnReduce( start, end );
	}

	protected abstract PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end );

	public PropertySignal<T> Smooth( MovieTime size ) => size <= 0d ? this : OnSmooth( size );
	protected virtual PropertySignal<T> OnSmooth( MovieTime size ) => this;

	/// <summary>
	/// Gets time ranges within the given <paramref name="timeRange"/> that have changing values.
	/// For painting in the timeline.
	/// </summary>
	public virtual IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange ) =>
		[timeRange];
}

/// <summary>
/// Extension methods for creating and composing <see cref="IPropertySignal"/>s.
/// </summary>
// ReSharper disable once UnusedMember.Global
public static partial class PropertySignalExtensions
{
	public static PropertySignal<T> Reduce<T>( this PropertySignal<T> signal, MovieTimeRange timeRange ) =>
		signal.Reduce( timeRange.Start, timeRange.End );

	public static T[] Sample<T>( this PropertySignal<T> signal, MovieTimeRange timeRange, int sampleRate )
	{
		var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );
		var samples = new T[sampleCount];

		for ( var i = 0; i < sampleCount; i++ )
		{
			var time = timeRange.Start + MovieTime.FromFrames( i, sampleRate );

			samples[i] = signal.GetValue( time );
		}

		return samples;
	}
}
