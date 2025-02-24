using Editor.MapEditor;
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

	private MovieBlockData ModifySamples( MovieBlock block, SamplesData<T> data, TimeSelection selection, T value, bool additive )
	{
		var interpolator = Interpolator.GetDefault<T>();
		var transformer = additive ? LocalTransformer.GetDefault<T>() : null;

		// Skip if time selection doesn't overlap this block

		var blockStart = block.StartTime;
		var blockEnd = block.StartTime + (block.Duration ?? block.Clip.Duration);

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
