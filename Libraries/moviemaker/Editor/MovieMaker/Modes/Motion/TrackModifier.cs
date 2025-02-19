using System.Linq;
using Editor.MapEditor;
using Sandbox.Diagnostics;
using Sandbox.MovieMaker;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
	public abstract MovieBlockData SampleTrack( MovieTrack track, float startTime, float duration, int sampleRate );
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

	public override MovieBlockData SampleTrack( MovieTrack track, float startTime, float duration, int sampleRate )
	{
		var endTime = startTime + duration;

		var samples = new T[(int)MathF.Ceiling( duration * sampleRate )];
		var cuts = new List<float> { startTime, endTime };

		foreach ( var block in track.Blocks )
		{
			if ( block.StartTime >= startTime && block.StartTime <= endTime )
			{
				cuts.Add( block.StartTime );
			}

			if ( block.Duration is { } blockDuration )
			{
				var blockEndTime = block.StartTime + blockDuration;

				if ( blockEndTime >= startTime && blockEndTime <= endTime )
				{
					cuts.Add( blockEndTime );
				}
			}
		}

		cuts.Sort();

		for ( var i = 0; i < cuts.Count - 1; ++i )
		{
			var t0 = cuts[i];
			var t1 = cuts[i + 1];

			if ( t1 - t0 <= 0f ) continue;

			var block = track.GetBlock( t0 ) ?? track.Blocks.LastOrDefault( x => x.Duration is { } d && x.StartTime + d == t0 );

			if ( block is null ) continue;

			SampleBlock( samples, startTime, t0, t1 - t0, sampleRate, block );
		}

		return new SamplesData<T>( sampleRate, SampleInterpolationMode.Linear, samples );
	}

	private void SampleBlock( T[] dstSamples, float timeOffset, float startTime, float duration, int sampleRate, MovieBlock block )
	{
		var dstOffset = (int)MathF.Round( (startTime - timeOffset) * sampleRate );
		var sampleCount = Math.Min( (int)MathF.Ceiling( duration * sampleRate ), dstSamples.Length - dstOffset );

		if ( sampleCount <= 0 ) return;

		switch ( block.Data )
		{
			case ConstantData<T> { Value: { } constValue }:
				Array.Fill( dstSamples, constValue, dstOffset, sampleCount );
				break;

			case SamplesData<T> { Samples.Count: > 0 } srcData:
				{
					if ( Math.Abs( srcData.SampleRate - sampleRate ) < 0.001f )
					{
						var srcOffset = (int)MathF.Round( (startTime - block.StartTime) * sampleRate );
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
						// TODO
					}

					break;
				}
		}
	}

	private MovieBlockData ModifySamples( MovieBlock block, SamplesData<T> data, TimeSelection selection, T value, bool additive )
	{
		var interpolator = Interpolator.GetDefault<T>();
		var transformer = additive ? LocalTransformer.GetDefault<T>() : null;

		// Skip if time selection doesn't overlap this block

		var blockStart = block.StartTime;
		var blockEnd = block.EndTime;

		if ( !selection.Overlaps( blockStart, blockEnd ) ) return data;

		var srcValues = data.Samples;
		var dstValues = new T[data.Samples.Count];

		var dt = 1f / data.SampleRate;

		for ( var i = 0; i < dstValues.Length; ++i )
		{
			var tLocal = i * dt;
			var fade = selection.GetFadeValue( block.StartTime + tLocal );

			var src = srcValues[i];
			var dst = transformer is not null ? transformer.ToGlobal( value, src ) : value;

			dstValues[i] = interpolator is null
				? fade >= 1f ? dst : src
				: interpolator.Interpolate( src, dst, fade );
		}

		return new SamplesData<T>( data.SampleRate, data.Interpolation, dstValues );
	}
}
