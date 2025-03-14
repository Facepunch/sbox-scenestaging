using System.Linq;
using System.Text.Json.Serialization;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public enum FadeDirection
{
	FadeIn,
	FadeOut
}

partial interface IProjectPropertyBlock
{
	IProjectPropertyBlock CrossFade( IProjectPropertyBlock next, InterpolationMode mode, FadeDirection direction = FadeDirection.FadeIn );
	IProjectPropertyBlock Blend( IProjectPropertyBlock overlay, TimeSelection envelope );
}

partial class PropertyBlock<T>
{
	public PropertyBlock<T> CrossFade( PropertyBlock<T> next, InterpolationMode mode = InterpolationMode.Linear, FadeDirection direction = FadeDirection.FadeIn )
	{
		var fadeRange = TimeRange with { End = next.TimeRange.End };

		return CrossFade( next, fadeRange, mode, direction );
	}

	public PropertyBlock<T> CrossFade( PropertyBlock<T> next, MovieTimeRange fadeRange, InterpolationMode mode = InterpolationMode.Linear, FadeDirection direction = FadeDirection.FadeIn )
	{
		var prev = this;

		if ( next.TimeRange.End < prev.TimeRange.Start )
		{
			throw new ArgumentException( "Can't cross fade to a block that ends before this block starts.", nameof( next ) );
		}

		var timeRange = prev.TimeRange with { End = next.TimeRange.End };

		// No fade if fadeRange starts after timeRange

		if ( fadeRange.Start > timeRange.End )
		{
			return prev;
		}

		// No fade if fadeRange ends before timeRange

		if ( fadeRange.End < timeRange.Start )
		{
			return next;
		}

		// If mode is None, or we can't interpolate this type, make fade range empty

		if ( Interpolator.GetDefault<T>() is null || mode == InterpolationMode.None )
		{
			fadeRange = direction == FadeDirection.FadeIn
				? fadeRange.End
				: fadeRange.Start;
		}

		// Hard join if fadeRange duration is zero

		if ( fadeRange.IsEmpty )
		{
			var before = prev.ClampEnd( fadeRange.Start );
			var after = next.ClampStart( fadeRange.End );

			return before.Join( after );
		}

		// Cross-fade each individual intersecting pair of sub-blocks

		return prev.Cut( fadeRange ).Zip( next.Cut( fadeRange ) )
			.Select( pair => CrossFadeCore( pair.Left, pair.Right, fadeRange, mode, direction ) )
			.Join();
	}

	private static PropertyBlock<T> CrossFadeCore( PropertyBlock<T> from, PropertyBlock<T> to, MovieTimeRange fadeRange, InterpolationMode mode, FadeDirection direction )
	{
		if ( from.TimeRange != to.TimeRange )
		{
			throw new ArgumentException( "Expected time ranges to exactly overlap.", nameof(to) );
		}

		if ( from == to )
		{
			return from;
		}

		if ( from.TimeRange.End <= fadeRange.Start )
		{
			return from;
		}

		if ( from.TimeRange.Start >= fadeRange.End )
		{
			return to;
		}

		return new PropertyBlockCrossFade<T>( from, to, fadeRange, mode, direction ).Reduce();
	}

	public PropertyBlock<T> Blend( PropertyBlock<T> overlay, TimeSelection envelope )
	{
		if ( envelope.TotalTimeRange.Intersect( TimeRange ) is null )
		{
			return this;
		}

		overlay = overlay.Slice( envelope.TotalTimeRange );

		return CrossFade( overlay, envelope.FadeInTimeRange, envelope.FadeIn.Interpolation )
			.CrossFade( this, envelope.FadeOutTimeRange, envelope.FadeOut.Interpolation, FadeDirection.FadeOut );
	}

	IProjectPropertyBlock IProjectPropertyBlock.CrossFade( IProjectPropertyBlock next, InterpolationMode mode, FadeDirection direction ) =>
		CrossFade( (PropertyBlock<T>)next, mode, direction );

	IProjectPropertyBlock IProjectPropertyBlock.Blend( IProjectPropertyBlock overlay, TimeSelection envelope ) =>
		Blend( (PropertyBlock<T>)overlay, envelope );
}

[JsonDiscriminator( "CrossFade" )]
file sealed class PropertyBlockCrossFade<T> : PropertyBlock<T>
{
	public MovieTimeRange FadeTimeRange { get; }
	public InterpolationMode Mode { get; }
	public FadeDirection Direction { get; }

	public PropertyBlock<T> From { get; }
	public PropertyBlock<T> To { get; }

	private readonly IInterpolator<T> _interpolator = Interpolator.GetDefault<T>()
		?? throw new Exception( $"Can't interpolate type {typeof(T)}." );

	public PropertyBlockCrossFade( PropertyBlock<T> from, PropertyBlock<T> to,
		MovieTimeRange fadeTimeRange, InterpolationMode mode, FadeDirection direction )
		: base( (from.TimeRange.Start, to.TimeRange.End) )
	{
		if ( mode == InterpolationMode.None )
		{
			throw new ArgumentException( $"Interpolation mode must not be {InterpolationMode.None}.",
				nameof( mode ) );
		}

		if ( from == to )
		{
			throw new ArgumentException( "From and To blocks must be different.",
				nameof( to ) );
		}

		if ( from.TimeRange != to.TimeRange )
		{
			throw new ArgumentException( "From and To blocks must exactly overlap.",
				nameof( to ) );
		}

		if ( !fadeTimeRange.Contains( from.TimeRange ) )
		{
			throw new ArgumentException( "Fade time range needs to contain the faded blocks' time range.",
				nameof( fadeTimeRange ) );
		}

		From = from;
		To = to;
		FadeTimeRange = fadeTimeRange;
		Mode = mode;
		Direction = direction;
	}

	public override T GetValue( MovieTime time )
	{
		var fraction = FadeTimeRange.GetFraction( time );
		var blend = Direction == FadeDirection.FadeIn
			? Mode.Apply( fraction )
			: 1f - Mode.Apply( 1f - fraction );

		if ( blend <= 0f )
		{
			return From.GetValue( time );
		}

		if ( blend >= 1f )
		{
			return To.GetValue( time );
		}

		return _interpolator.Interpolate( From.GetValue( time ), To.GetValue( time ), blend );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return From.Shift( offset ).CrossFade( To.Shift( offset ), FadeTimeRange + offset, Mode, Direction );
	}

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return From.Slice( timeRange ).CrossFade( To.Slice( timeRange ), FadeTimeRange, Mode, Direction );
	}

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return From.GetPaintHintTimes( timeRange )
			.Merge( To.GetPaintHintTimes( timeRange ) )
			.Merge( IPropertyBlock.GetSampleTimes( TimeRange, TimeRange.Start, int.MaxValue, 30 ) );
	}
}
