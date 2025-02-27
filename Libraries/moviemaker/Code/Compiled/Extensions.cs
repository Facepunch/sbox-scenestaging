namespace Sandbox.MovieMaker.Compiled;

#nullable enable

/// <summary>
/// Helper methods for working with <see cref="Clip"/>, <see cref="Track"/>, or <see cref="Block"/>.
/// </summary>
public static class CompiledClipExtensions
{
	public static PropertyTrack<T> WithConstant<T>( this PropertyTrack<T> track,
		MovieTimeRange timeRange, T value )
	{
		return track with { Blocks = [..track.Blocks, new ConstantBlock<T>( timeRange, value )] };
	}

	public static PropertyTrack<T> WithSamples<T>( this PropertyTrack<T> track,
		MovieTimeRange timeRange, int sampleRate, params IEnumerable<T> values )
	{
		return track with
		{
			Blocks = [.. track.Blocks, new SampleBlock<T>( timeRange, MovieTime.Zero, sampleRate, [..values] )]
		};
	}
}
