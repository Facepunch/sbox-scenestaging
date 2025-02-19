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

	public abstract MovieBlockData Modify( MovieBlock block, MovieBlockData data, TimeSelection selection, object? value, bool additive );
	public abstract MovieBlockData SampleTrack( MovieTrack track, MovieTimeRange timeRange, int sampleRate );
}

internal sealed class TrackModifier<T> : TrackModifier
{
	public override MovieBlockData Modify( MovieBlock block, MovieBlockData data, TimeSelection selection, object? value, bool additive )
	{
		Assert.AreEqual( typeof(T), block.Track.PropertyType );

		return data switch
		{
			// TODO
			IConstantData => data,
			SamplesData<T> samples => ModifySamples( block, samples, selection, (T) value!, additive ),
			_ => data
		};
	}

	public override MovieBlockData SampleTrack( MovieTrack track, MovieTimeRange timeRange, int sampleRate )
	{
		var samples = new T[timeRange.Duration.GetFrameCount( sampleRate )];

		// TODO: make this more generic? what do we do with scale?

		if ( Rotation.Identity is T defaultValue )
		{
			Array.Fill( samples, defaultValue );
		}

		if ( track.Cuts is { Count: > 0 } cuts )
		{
			if ( timeRange.Start < cuts[0].TimeRange.Start )
			{
				SampleBlock( samples, timeRange, cuts[0].Block, (timeRange.Start, cuts[0].TimeRange.Start), sampleRate );
			}

			foreach ( var cut in track.Cuts )
			{
				SampleBlock( samples, timeRange, cut.Block, cut.TimeRange, sampleRate );
			}

			if ( timeRange.End > cuts[^1].TimeRange.End )
			{
				SampleBlock( samples, timeRange, cuts[^1].Block, (cuts[^1].TimeRange.End, timeRange.End), sampleRate );
			}
		}


		return new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, samples );
	}

	private void SampleBlock( T[] dstSamples, MovieTimeRange dstTimeRange, MovieBlock block, MovieTimeRange srcTimeRange, int sampleRate )
	{
		if ( dstTimeRange.Intersect( srcTimeRange ) is not { } intersection ) return;

		var dstOffset = (intersection.Start - dstTimeRange.Start).GetFrameCount( sampleRate );
		var sampleCount = intersection.Duration.GetFrameCount( sampleRate );

		if ( sampleCount <= 0 ) return;

		switch ( block.Data )
		{
			case ConstantData<T> { Value: { } constValue }:
				Array.Fill( dstSamples, constValue, dstOffset, sampleCount );
				break;

			case SamplesData<T> { Samples.Count: > 0 } srcData:
				{
					if ( srcData.SampleRate == sampleRate )
					{
						var srcOffset = (intersection.Start - block.Start).GetFrameCount( sampleRate );
						var srcSampleCount = Math.Min( sampleCount, srcData.Samples.Count - srcOffset );

						for ( var i = Math.Max( 0, -srcOffset ); i < srcSampleCount; ++i )
						{
							dstSamples[dstOffset + i] = srcData.Samples[srcOffset + i];
						}

						if ( srcSampleCount < sampleCount )
						{
							Array.Fill( dstSamples, srcData.Samples[^1], dstOffset + srcSampleCount, sampleCount - srcSampleCount );
						}
					}
					else
					{
						throw new NotImplementedException();
					}

					break;
				}
		}
	}

	private MovieBlockData ModifySamples( MovieBlock block, SamplesData<T> data, TimeSelection selection, T value, bool additive )
	{
		var interpolator = Interpolator.GetDefault<T>();
		var transformer = additive ? LocalTransformer.GetDefault<T>() : null;

		if ( selection.GetTimeRange( block.Clip ).Intersect( block.TimeRange ) is not { } intersection ) return data;

		var srcValues = data.Samples;
		var dstValues = new T[data.Samples.Count];

		for ( var i = 0; i < dstValues.Length; ++i )
		{
			var time = block.Start + MovieTime.FromFrames( i, data.SampleRate );
			var fade = selection.GetFadeValue( time );

			var src = srcValues[i];
			var dst = transformer is not null ? transformer.ToGlobal( value, src ) : value;

			dstValues[i] = interpolator is null
				? fade >= 1f ? dst : src
				: interpolator.Interpolate( src, dst, fade );
		}

		return new SamplesData<T>( data.SampleRate, data.Interpolation, dstValues );
	}
}
