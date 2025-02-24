using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker.Compiled;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

[JsonDiscriminator( "Source" )]
[method: JsonConstructor]
file sealed record CompiledSignal<T>( ProjectSourceClip Source, int TrackIndex, int BlockIndex,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] MovieTransform Transform = default,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] MovieTime SmoothingSize = default ) : PropertySignal<T>
{
	private ImmutableArray<T>? _samples;
	private CompiledSampleBlock<T>? _block;

	private CompiledSampleBlock<T> Block => _block ??= (CompiledSampleBlock<T>)((CompiledPropertyTrack<T>)Source.Clip.Tracks[TrackIndex]).Blocks[BlockIndex];

	private ImmutableArray<T> Samples => _samples ??= Block.Resample( Block.SampleRate, SmoothingSize, _interpolator );

	public CompiledSignal( CompiledSignal<T> copy )
		: base( copy )
	{
		Source = copy.Source;
		TrackIndex = copy.TrackIndex;
		BlockIndex = copy.BlockIndex;
		Transform = copy.Transform;
		SmoothingSize = copy.SmoothingSize;

		_samples = null;
		_block = null;
	}

	private MovieTime GetLocalTime( MovieTime time ) =>
		(Transform.Inverse * time).Clamp( Block.TimeRange ) - Block.TimeRange.Start - Block.Offset;

	public override T GetValue( MovieTime time ) =>
		Samples.Sample( GetLocalTime( time ), Block.SampleRate, _interpolator );

	protected override PropertySignal<T> OnTransform( MovieTransform transform ) =>
		this with { Transform = transform * Transform };

	protected override PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end )
	{
		if ( start is { } s && GetLocalTime( s ) >= Block.TimeRange.Duration ) return Block.GetValue( Block.TimeRange.End );
		if ( end is { } e && GetLocalTime( e ) <= 0d ) return Block.GetValue( Block.TimeRange.Start );

		return this;
	}

	protected override PropertySignal<T> OnSmooth( MovieTime size ) =>
		_interpolator is null ? this : this with { SmoothingSize = size };

	public override IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange )
	{
		if ( timeRange.Intersect( Transform * Block.TimeRange ) is { } intersection )
		{
			return [intersection];
		}

		return [];
	}

	protected override bool PrintMembers( StringBuilder builder )
	{
		builder.Append( $"Source = {Source}, " );
		builder.Append( $"Block = {Block.TimeRange}" );

		if ( Transform != MovieTransform.Identity )
		{
			builder.Append( $", Transform = {Transform}" );
		}

		if ( SmoothingSize != default )
		{
			builder.Append( $", SmoothingSize = {SmoothingSize}" );
		}

		return true;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine( Source, TrackIndex, BlockIndex, Transform, SmoothingSize );
	}

	public bool Equals( CompiledSignal<T>? other )
	{
		if ( other is null ) return false;

		return Source.Equals( other.Source )
			&& TrackIndex == other.TrackIndex
			&& BlockIndex == other.BlockIndex
			&& Transform == other.Transform
			&& SmoothingSize == other.SmoothingSize;
	}

	private static readonly IInterpolator<T>? _interpolator = Interpolator.GetDefault<T>();
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

		var trackIndex = source.Clip.Tracks.IndexOf( matchingTrack );

		return matchingTrack.Blocks
			.Select( (x, i) => new PropertyBlock<T>( x switch
			{
				CompiledConstantBlock<T> constant => constant.Value,
				CompiledSampleBlock<T> => new CompiledSignal<T>( source, trackIndex, i ),
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
