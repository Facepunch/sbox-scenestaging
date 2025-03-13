using System.Collections.Immutable;
using System.Linq;
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
public interface IProjectPropertyBlock : IPropertyBlock
{
	IProjectPropertyBlock Slice( MovieTimeRange timeRange );
	IProjectPropertyBlock Shift( MovieTime offset );
	IProjectPropertyBlock Join( IProjectPropertyBlock next, bool smooth );
	IProjectPropertyBlock CrossFade( IProjectPropertyBlock next, InterpolationMode mode, bool invert );
}

[JsonPolymorphic]
public abstract partial record PropertyBlock<T>( MovieTimeRange TimeRange ) : IPropertyBlock<T>, IProjectPropertyBlock
{
	public abstract T GetValue( MovieTime time );

	public IEnumerable<MovieTime> GetPaintHintTimes( MovieTimeRange timeRange ) =>
		OnGetPaintHintTimes( timeRange.Clamp( TimeRange ) );

	protected virtual IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		yield return timeRange.Start;
		yield return timeRange.End;
	}

	public PropertyBlock<T> Blend( PropertyBlock<T> overlay, TimeSelection envelope )
	{
		if ( envelope.TotalTimeRange.Intersect( TimeRange ) is null )
		{
			return this;
		}

		overlay = overlay.Slice( envelope.TotalTimeRange );

		return this.CrossFade( overlay, envelope.FadeInTimeRange, envelope.FadeIn.Interpolation, false )
			.CrossFade( this, envelope.FadeOutTimeRange, envelope.FadeOut.Interpolation, true );
	}

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

	public PropertyBlock<T>? TryMerge( PropertyBlock<T> next ) => OnTryMerge( next );

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

public sealed record SourceClipPropertyBlock<T>( ProjectSourceClip Source, CompiledPropertyTrack<T> Track, CompiledPropertyBlock<T> Block )
	: PropertyBlock<T>( Block.TimeRange )
{
	public override T GetValue( MovieTime time ) => Block.GetValue( time );

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		switch ( Block )
		{
			case CompiledSampleBlock<T> sampleBlock:
				return IProjectPropertyBlock.GetSampleTimes( timeRange, sampleBlock.TimeRange.Start + sampleBlock.Offset,
					sampleBlock.Samples.Length, sampleBlock.SampleRate );

			default:
				return [timeRange.Start, timeRange.End - MovieTime.Epsilon];
		}
	}

	public override string ToString()
	{
		return $"SourceClip {{ Source = {Source.Id}, TrackName = {Track.Name}, TimeRange = {Block.TimeRange} }}";
	}
}

public sealed record ConstantPropertyBlock<T>( MovieTimeRange TimeRange, T Value ) : PropertyBlock<T>( TimeRange )
{
	public override T GetValue( MovieTime time ) => Value;

	public override CompiledPropertyBlock<T> Compile( ProjectTrack track ) =>
		new CompiledConstantBlock<T>( TimeRange, Value );

	public override string ToString()
	{
		return $"Constant {{ Value = {Value}, TimeRange = {TimeRange} }}";
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return [timeRange.Start, timeRange.End - MovieTime.Epsilon];
	}
}
