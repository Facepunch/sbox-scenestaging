using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Editor.MovieMaker;

#nullable enable

public interface IPropertyBlock
{
	MovieTimeRange TimeRange { get; }
	Type PropertyType { get; }

	object? GetValue( MovieTime time );

	IEnumerable<MovieTime> GetPaintHintTimes( MovieTimeRange timeRange );

	public static IEnumerable<MovieTime> GetSampleTimes( MovieTimeRange timeRange, MovieTime firstSampleTime, int sampleCount, int sampleRate )
	{
		var firstIndex = Math.Max( 0, (timeRange.Start - firstSampleTime).GetFrameIndex( sampleRate ) );
		var lastIndex = Math.Min( sampleCount, (timeRange.End - firstSampleTime).GetFrameCount( sampleRate ) );

		return Enumerable.Range( firstIndex, lastIndex - firstIndex )
			.Select( i => firstSampleTime + MovieTime.FromFrames( i, sampleRate ) );
	}
}

public interface IDynamicBlock
{
	event Action? Changed;
}

public interface IPropertyBlock<out T> : IPropertyBlock
{
	new T GetValue( MovieTime time );

	object? IPropertyBlock.GetValue( MovieTime time ) => GetValue( time );
	Type IPropertyBlock.PropertyType => typeof(T);
}

/// <summary>
/// A <see cref="IPropertyBlock"/> that can be edited with methods like <see cref="Slice"/>, <see cref="Shift"/> and <see cref="Join"/>.
/// </summary>
public partial interface IProjectPropertyBlock : IPropertyBlock
{
	JsonNode? Serialize();
}

public abstract partial class PropertyBlock<T> : IPropertyBlock<T>, IProjectPropertyBlock
{
	[JsonIgnore]
	public MovieTimeRange TimeRange { get; }

	protected PropertyBlock( MovieTimeRange timeRange )
	{
		TimeRange = timeRange;
	}

	public abstract T GetValue( MovieTime time );

	public IEnumerable<MovieTime> GetPaintHintTimes( MovieTimeRange timeRange ) =>
		OnGetPaintHintTimes( timeRange.Clamp( TimeRange ) )
			.Merge( [timeRange.Start, timeRange.End - MovieTime.Epsilon] );

	protected virtual IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange ) => [];

	public PropertyBlock<T> Reduce()
	{
		var block = this;

		while ( block.OnReduce() is { } reduced && reduced != block )
		{
			block = reduced;
		}

		return block;
	}

	protected virtual PropertyBlock<T> OnReduce() => this;

	public PropertyBlock<T>? TryMerge( PropertyBlock<T> next ) => OnTryMerge( next )?.Reduce();

	protected virtual PropertyBlock<T>? OnTryMerge( PropertyBlock<T> next ) => null;

	public virtual CompiledPropertyBlock<T> Compile( ProjectTrack track )
	{
		var sampleRate = track.Project.SampleRate;
		var sampleCount = TimeRange.Duration.GetFrameCount( sampleRate );

		var samples = new T[sampleCount];

		for ( var i = 0; i < sampleCount; ++i )
		{
			var time = TimeRange.Start + MovieTime.FromFrames( i, sampleCount );

			samples[i] = GetValue( time );
		}

		return new CompiledSampleBlock<T>( TimeRange, TimeRange.Start, sampleRate, [..samples] );
	}
}
