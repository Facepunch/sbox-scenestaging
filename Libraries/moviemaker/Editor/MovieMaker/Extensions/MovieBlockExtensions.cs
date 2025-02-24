using Sandbox.MovieMaker;
using System.Linq;

namespace Editor.MovieMaker;

#nullable enable

public record struct MovieBlockSlice( MovieTimeRange TimeRange, IBlockData Data ) : IMovieBlock;

public interface IPreviewMovieBlock : IMovieBlock
{
	event Action? Changed;
}

internal static class MovieBlockExtensions
{
	public static MovieTime Start<T>( this T block ) where T : IMovieBlock => block.TimeRange.Start;
	public static MovieTime End<T>( this T block ) where T : IMovieBlock => block.TimeRange.End;
	public static MovieTime Duration<T>( this T block ) where T : IMovieBlock => block.TimeRange.Duration;

	public static void Sample( this IMovieBlock block, Array dstSamples, MovieTimeRange srcTimeRange, MovieTimeRange dstTimeRange, int sampleRate )
	{
		if ( block.Data is not IValueData valueData ) return;

		var dstStartIndex = dstTimeRange.Start.GetFrameIndex( sampleRate );
		var dstEndIndex = dstStartIndex + dstTimeRange.Duration.GetFrameCount( sampleRate );

		if ( dstStartIndex < 0 )
		{
			srcTimeRange = (srcTimeRange.Start + MovieTime.FromFrames( -dstStartIndex, sampleRate ), srcTimeRange.End);
			dstStartIndex = 0;
		}

		if ( dstEndIndex > dstSamples.Length )
		{
			srcTimeRange = (srcTimeRange.Start, srcTimeRange.End - MovieTime.FromFrames( dstEndIndex - dstSamples.Length, sampleRate ));
			dstEndIndex = dstSamples.Length;
		}

		if ( dstEndIndex <= dstStartIndex || srcTimeRange.IsEmpty ) return;

		valueData.Sample( dstSamples, dstStartIndex, dstEndIndex - dstStartIndex, srcTimeRange - block.Start(), sampleRate );
	}

	public static IConstantData CreateConstantData( this Type type, object? value )
	{
		return (IConstantData)Activator.CreateInstance( typeof( ConstantData<> ).MakeGenericType( type ), value )!;
	}

	public static ISamplesData CreateSamplesData( this Type type,
		int sampleRate,
		Array samples,
		MovieTime firstSampleTime = default )
	{
		return (ISamplesData)Activator.CreateInstance( typeof( SamplesData<> ).MakeGenericType( type ),
			sampleRate, samples, firstSampleTime )!;
	}

	public static IEnumerable<MovieBlockSlice> Slice( this MovieTrack track, MovieTimeRange timeRange ) =>
		track.GetCuts( timeRange ).Select( x => x.Block.Slice( x.TimeRange ) );

	public static MovieBlockSlice Slice( this MovieBlock srcBlock, MovieTimeRange timeRange ) =>
		new( timeRange, srcBlock.Data.Slice( timeRange - srcBlock.Start() ) );

	public static IBlockData Slice( this IBlockData data, MovieTimeRange timeRange )
	{
		return data is not IValueData valueData ? data : valueData.Slice( timeRange );
	}

	public static void Sample( this IValueData data,
		Array dstSamples, int dstOffset, int sampleCount,
		MovieTimeRange srcTimeRange, int sampleRate )
	{
		BlockDataHelper.Get( data.ValueType )
			.Sample( data, dstSamples, dstOffset, sampleCount, srcTimeRange, sampleRate );
	}


	public static void Sample<T>( this IValueData<T> data, Span<T> dstSamples, MovieTimeRange srcTimeRange, int sampleRate )
	{
		switch ( data )
		{
			case ConstantData<T> constant:
			{
				dstSamples.Fill( constant.Value );
				break;
			}

			// TODO: can do a fast path for SamplesData<T> if same sample rate / phase

			default:
			{
				for ( var i = 0; i < dstSamples.Length; ++i )
				{
					var time = srcTimeRange.Start + MovieTime.FromFrames( i, sampleRate );

					dstSamples[i] = data.GetValue( time );
				}

				break;
			}
		}
	}

	public static IValueData Slice( this IValueData data, MovieTimeRange timeRange )
	{
		return BlockDataHelper.Get( data.ValueType )
			.Slice( data, timeRange );
	}

	public static IValueData<T> Slice<T>( this IValueData<T> data, MovieTimeRange timeRange )
	{
		return data switch
		{
			ConstantData<T> constant => constant,
			SamplesData<T> samples => samples.Slice( timeRange ),
			_ => throw new NotImplementedException()
		};
	}

	public static IValueData<T> Slice<T>( this SamplesData<T> data, MovieTimeRange timeRange )
	{
		var sampleRate = data.SampleRate;
		var samples = data.Samples;

		if ( samples.Length == 0 ) return data;

		timeRange -= data.Offset;

		var i0 = timeRange.Start.GetFrameIndex( sampleRate, out var remainder );
		var i1 = i0 + timeRange.Duration.GetFrameCount( sampleRate );

		// Constants if we're off one end

		if ( i1 <= 0 ) return new ConstantData<T>( samples[0] );
		if ( i0 >= samples.Length ) return new ConstantData<T>( samples[^1] );

		var firstSampleTime = -remainder;

		if ( i0 < 0 )
		{
			firstSampleTime += MovieTime.FromFrames( -i0, sampleRate );
			i0 = 0;
		}

		i1 = Math.Clamp( i1, i0, samples.Length );

		return new SamplesData<T>( sampleRate, samples.Slice( i0, i1 - i0 ), firstSampleTime );
	}
}

file abstract class BlockDataHelper
{
	[SkipHotload]
	private static Dictionary<Type, BlockDataHelper> Cache { get; } = new();

	[EditorEvent.Hotload]
	private void OnHotload()
	{
		Cache.Clear();
	}

	public static BlockDataHelper Get( Type type )
	{
		if ( Cache.TryGetValue( type, out var helper ) )
		{
			return helper;
		}

		return Cache[type] = (BlockDataHelper)Activator.CreateInstance( typeof(BlockDataHelper<>).MakeGenericType( type ) )!;
	}

	public abstract void Sample( IValueData data, Array dstSamples, int dstOffset, int sampleCount, MovieTimeRange srcTimeRange, int sampleRate );
	public abstract IValueData Slice( IValueData data, MovieTimeRange timeRange );
}

file sealed class BlockDataHelper<T> : BlockDataHelper
{
#pragma warning disable SB3000
	public static BlockDataHelper<T> Instance { get; } = new ();
#pragma warning restore SB3000

	public override void Sample( IValueData data,
		Array dstSamples, int dstOffset, int sampleCount,
		MovieTimeRange srcTimeRange, int sampleRate )
	{
		var span = ((T[])dstSamples).AsSpan( dstOffset, sampleCount );

		((IValueData<T>)data).Sample( span, srcTimeRange, sampleRate );
	}

	public override IValueData Slice( IValueData data, MovieTimeRange timeRange )
	{
		return ((IValueData<T>)data).Slice( timeRange );
	}
}
