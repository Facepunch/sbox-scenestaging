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

	public abstract IMovieBlockData Blend( IMovieBlock original, IMovieBlock change, MovieTimeRange timeRange, TimeSelection selection, bool additive, int sampleRate );
}

internal sealed class TrackModifier<T> : TrackModifier
{
	public override IMovieBlockData Blend( IMovieBlock original, IMovieBlock change, MovieTimeRange timeRange, TimeSelection selection, bool additive, int sampleRate )
	{
		if ( original.Data is not IMovieBlockValueData<T> originalData ) return original.Data.Slice( timeRange );
		if ( change.Data is not IMovieBlockValueData<T> changeData ) return original.Data.Slice( timeRange );

		if ( additive ) throw new NotImplementedException();

		var interpolator = Interpolator.GetDefault<T>();
		var transformer = additive ? LocalTransformer.GetDefault<T>() : null;

		var sampleCount = timeRange.Duration.GetFrameCount( sampleRate );

		var dstValues = new T[sampleCount];

		originalData.Sample( dstValues, timeRange - original.TimeRange.Start, sampleRate );

		for ( var i = 0; i < sampleCount; ++i )
		{
			var time = timeRange.Start + MovieTime.FromFrames( i, sampleRate );
			var fade = selection.GetFadeValue( time );

			var src = dstValues[i];
			var dst = changeData.GetValue( time - change.TimeRange.Start );

			// todo: additive

			dstValues[i] = interpolator is null
				? fade >= 1f ? dst : src
				: interpolator.Interpolate( src, dst, fade );
		}

		return new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, dstValues );
	}
}
