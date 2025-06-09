using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public abstract partial record PropertySignal : IPropertySignal
{
	[JsonIgnore]
	public abstract Type PropertyType { get; }

	protected PropertySignal( PropertySignal copy )
	{
		// Empty so any lazily computed fields aren't copied
	}

	object? IPropertySignal.GetValue( MovieTime time ) => OnGetValue( time );

	protected abstract object? OnGetValue( MovieTime time );

	/// <summary>
	/// Gets time ranges within the given <paramref name="timeRange"/> that have changing values.
	/// For painting in the timeline.
	/// </summary>
	public virtual IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange ) => [timeRange];
}

/// <summary>
/// A <see cref="IPropertySignal{T}"/> that can be composed with <see cref="PropertyOperation{T}"/>s,
/// and stored in a <see cref="IPropertyBlock{T}"/>.
/// </summary>
public abstract partial record PropertySignal<T>() : PropertySignal, IPropertySignal<T>
{
	[JsonIgnore]
	public sealed override Type PropertyType => typeof(T);

	protected PropertySignal( PropertySignal<T> copy )
		: base( copy )
	{
		// Empty so any lazily computed fields aren't copied
	}

	public abstract T GetValue( MovieTime time );

	protected sealed override object? OnGetValue( MovieTime time ) => GetValue( time );

	/// <summary>
	/// Try to make a more minimal composition for this signal, optionally within a time range.
	/// </summary>
	/// <param name="start">Optional start time, we can discard any features before this if given.</param>
	/// <param name="end">Optional end time, we can discard any features after this if given.</param>
	public PropertySignal<T> Reduce( MovieTime? start = null, MovieTime? end = null )
	{
		return start >= end && !GetKeyframes( start.Value ).Any() ? GetValue( start.Value ) : OnReduce( start, end );
	}

	public PropertySignal<T> Reduce( MovieTimeRange timeRange ) =>
		Reduce( timeRange.Start, timeRange.End );

	public T[] Sample( MovieTimeRange timeRange, int sampleRate )
	{
		var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );
		var samples = new T[sampleCount];

		for ( var i = 0; i < sampleCount; i++ )
		{
			var time = timeRange.Start + MovieTime.FromFrames( i, sampleRate );

			samples[i] = GetValue( time );
		}

		return samples;
	}

	protected abstract PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end );

	public PropertySignal<T> Smooth( MovieTime size ) => size <= 0d ? this : OnSmooth( size );
	protected virtual PropertySignal<T> OnSmooth( MovieTime size ) => this;
}

/// <summary>
/// Extension methods for creating and composing <see cref="IPropertySignal"/>s.
/// </summary>
// ReSharper disable once UnusedMember.Global
public static partial class PropertySignalExtensions;
