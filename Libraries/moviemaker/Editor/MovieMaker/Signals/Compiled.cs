using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "Compiled" )]
file sealed record CompiledSignal<T>( ProjectSourceClip Source, CompiledSampleBlock<T> Block,
	MovieTime Offset = default,
	MovieTime SmoothingSize = default ) : PropertySignal<T>
{
	private readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();

	private ImmutableArray<T>? _samples;

	private ImmutableArray<T> Samples => _samples ??= Block.Resample( Block.SampleRate, SmoothingSize, _interpolator );

	public override bool CanSmooth => _interpolator is not null;

	public override T GetValue( MovieTime time )
	{
		var localTime = (time - Offset).Clamp( Block.TimeRange ) - Block.TimeRange.Start - Block.Offset;

		return Samples.Sample( localTime, Block.SampleRate, _interpolator );
	}

	protected override PropertySignal<T> OnTransform( MovieTime offset ) =>
		this with { Offset = Offset + offset };

	protected override PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end )
	{
		if ( start >= Block.TimeRange.End + Offset ) return Block.GetValue( Block.TimeRange.End );
		if ( end <= Block.TimeRange.Start + Offset ) return Block.GetValue( Block.TimeRange.Start );

		return this;
	}

	protected override PropertySignal<T> OnSmooth( MovieTime size ) =>
		this with { SmoothingSize = size, _samples = null };

	public override IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange )
	{
		if ( timeRange.Intersect( Block.TimeRange + Offset ) is { } intersection )
		{
			return [intersection];
		}

		return [];
	}

	protected override bool PrintMembers( StringBuilder builder )
	{
		builder.Append( $"Source = {Source}, " );
		builder.Append( $"Block = {Block.TimeRange}" );

		if ( Offset != default )
		{
			builder.Append( $", Offset = {Offset}" );
		}

		if ( SmoothingSize != default )
		{
			builder.Append( $", SmoothingSize = {SmoothingSize}" );
		}

		return true;
	}
}

partial class PropertySignalExtensions
{
	public static IReadOnlyList<PropertyBlock<T>> AsBlocks<T>( this ProjectSourceClip source, IProjectPropertyTrack track )
	{
		var (refTrack, propertyPath) = track.GetPath();

		if ( source.Clip.GetProperty<T>( refTrack.Id, propertyPath ) is not { } matchingTrack )
		{
			return [];
		}

		return matchingTrack.Blocks
			.Select( x => new PropertyBlock<T>( x switch
			{
				CompiledConstantBlock<T> constant => constant.Value,
				CompiledSampleBlock<T> sample => new CompiledSignal<T>( source, sample ),
				_ => throw new NotImplementedException()
			}, x.TimeRange ) )
			.ToImmutableArray();
	}

	public static ImmutableArray<T> Resample<T>( this CompiledSampleBlock<T> source, int sampleRate,
		MovieTime smoothingSize, IInterpolator<T>? interpolator )
	{
		if ( interpolator is null )
		{
			smoothingSize = default;
		}

		if ( sampleRate == source.SampleRate && smoothingSize <= 0d )
		{
			return source.Samples;
		}

		var sampleCount = sampleRate == source.SampleRate
			? source.Samples.Length
			: source.TimeRange.Duration.GetFrameCount( sampleRate );

		var samples = new T[sampleCount];
		var sourceSamples = source.Samples;

		if ( sampleRate == source.SampleRate )
		{
			sourceSamples.CopyTo( samples );
		}
		else
		{
			for ( var i = 0; i < sampleCount; i++ )
			{
				var t = MovieTime.FromFrames( i, sampleRate );
				samples[i] = sourceSamples.Sample( t, sampleRate, interpolator );
			}
		}

		if ( smoothingSize <= 0d || interpolator is null )
		{
			return [..samples];
		}

		var smoothingPasses = smoothingSize.GetFrameCount( sampleRate );

		T[] back = samples, front = [..samples];

		for ( var pass = 0; pass < smoothingPasses; pass++ )
		{
			for ( var i = 0; i < sampleCount; i++ )
			{
				var prev = back[Math.Max( 0, i - 1 )];
				var curr = back[i];
				var next = back[Math.Min( sampleCount - 1, i + 1 )];

				var prevCurr = interpolator.Interpolate( prev, curr, 0.5f );
				var currNext = interpolator.Interpolate( curr, next, 0.5f );

				front[i] = interpolator.Interpolate( prevCurr, currNext, 0.5f );
			}

			(back, front) = (front, back);
		}

		return [..back];
	}
}
