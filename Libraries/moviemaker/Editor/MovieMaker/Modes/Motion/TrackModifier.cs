using System.Linq;
using Sandbox.Diagnostics;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

internal abstract class TrackModifier
{
	[SkipHotload]
	private static Dictionary<Type, TrackModifier?> Cache { get; } = new();

	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		Cache.Clear();
	}

	public static TrackModifier? Get( Type type )
	{
		if ( Cache.TryGetValue( type, out var cached ) ) return cached;

		return Cache[type] = (TrackModifier)Activator.CreateInstance( typeof(TrackModifier<>).MakeGenericType( type ) )!;
	}

	public abstract ISamplesData SampleTrack( MovieTrack track, MovieTimeRange timeRange, int sampleRate );
	public abstract ISamplesData Modify( ISamplesData srcData, MovieTimeRange srcTimeRange, TimeSelection selection, object? value, bool additive );
}

internal sealed class TrackModifier<T> : TrackModifier
{
	public override ISamplesData SampleTrack( MovieTrack track, MovieTimeRange timeRange, int sampleRate )
	{
		var samples = new T[timeRange.Duration.GetFrameCount( sampleRate )];

		// TODO: make this more generic? what do we do with scale?

		if ( Rotation.Identity is T defaultValue )
		{
			Array.Fill( samples, defaultValue );
		}

		if ( track.Cuts is not { Count: > 0 } cuts )
		{
			return new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, samples );
		}

		// Fill before first cut

		cuts[0].Block.Sample<T>( samples, timeRange, (timeRange.Start, cuts[0].TimeRange.Start), sampleRate );
		
		// Fill within each cut
		
		foreach ( var cut in track.Cuts )
		{
			cut.Block.Sample<T>( samples, timeRange, cut.TimeRange, sampleRate );
		}

		// Fill after last cut

		cuts[^1].Block.Sample<T>( samples, timeRange, (cuts[^1].TimeRange.End, timeRange.End), sampleRate );

		return new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, samples );
	}

	public override ISamplesData Modify( ISamplesData srcData, MovieTimeRange srcTimeRange, TimeSelection selection, object? value, bool additive )
	{
		return Modify( (SamplesData<T>)srcData, srcTimeRange, selection, (T)value!, additive );
	}

	private ISamplesData Modify( SamplesData<T> srcData, MovieTimeRange srcTimeRange, TimeSelection selection, T value, bool additive )
	{
		var interpolator = Interpolator.GetDefault<T>();
		var transformer = additive ? LocalTransformer.GetDefault<T>() : null;

		if ( selection.GetTimeRange( srcTimeRange ).Intersect( srcTimeRange ) is not { } intersection ) return srcData;

		var srcValues = srcData.Samples;
		var dstValues = new T[srcValues.Count];

		for ( var i = 0; i < dstValues.Length; ++i )
		{
			var time = srcTimeRange.Start + MovieTime.FromFrames( i, srcData.SampleRate );
			var fade = selection.GetFadeValue( time );

			var src = srcValues[i];
			var dst = transformer is not null ? transformer.ToGlobal( value, src ) : value;

			dstValues[i] = interpolator is null
				? fade >= 1f ? dst : src
				: interpolator.Interpolate( src, dst, fade );
		}

		return new SamplesData<T>( srcData.SampleRate, srcData.Interpolation, dstValues );
	}
}
