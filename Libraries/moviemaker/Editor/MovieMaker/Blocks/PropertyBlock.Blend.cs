using System.Linq;
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
	IProjectPropertyBlock Blend( IProjectPropertyBlock overlay, float alpha );
	IProjectPropertyBlock CrossFade( IProjectPropertyBlock next, InterpolationMode mode, FadeDirection direction = FadeDirection.FadeIn );
	IProjectPropertyBlock CrossFade( IProjectPropertyBlock overlay, TimeSelection envelope );
}

partial class PropertyBlock<T>
{
	public PropertyBlock<T> Blend( PropertyBlock<T> overlay, float alpha )
	{
		if ( overlay.TimeRange != TimeRange )
		{
			throw new ArgumentException( "Blend must be between two exactly overlapping blocks.", nameof(overlay) );
		}

		if ( overlay.Equals( this ) ) return this;
		if ( alpha <= 0f ) return this;
		if ( alpha >= 1f ) return overlay;
		if ( Interpolator.GetDefault<T>() is not { } interpolator ) return this;

		return OnBlend( overlay, alpha, interpolator );
	}

	protected virtual PropertyBlock<T> OnBlend( PropertyBlock<T> overlay, float alpha, IInterpolator<T> interpolator ) =>
		new PropertyBlockBlend<T>( this, overlay, alpha ).Reduce();

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

			return before + after;
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

		if ( from.Equals( to ) )
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

	public PropertyBlock<T> CrossFade( PropertyBlock<T> overlay, TimeSelection envelope )
	{
		if ( envelope.TotalTimeRange.Intersect( TimeRange ) is null )
		{
			return this;
		}

		overlay = overlay.Slice( envelope.TotalTimeRange );

		return CrossFade( overlay, envelope.FadeInTimeRange, envelope.FadeIn.Interpolation )
			.CrossFade( this, envelope.FadeOutTimeRange, envelope.FadeOut.Interpolation, FadeDirection.FadeOut );
	}

	IProjectPropertyBlock IProjectPropertyBlock.Blend( IProjectPropertyBlock overlay, float alpha ) =>
		Blend( (PropertyBlock<T>)overlay, alpha );

	IProjectPropertyBlock IProjectPropertyBlock.CrossFade( IProjectPropertyBlock next, InterpolationMode mode, FadeDirection direction ) =>
		CrossFade( (PropertyBlock<T>)next, mode, direction );

	IProjectPropertyBlock IProjectPropertyBlock.CrossFade( IProjectPropertyBlock overlay, TimeSelection envelope ) =>
		CrossFade( (PropertyBlock<T>)overlay, envelope );
}

file abstract class PropertyBlockInterpolator<T> : PropertyBlock<T>
{
	private readonly IInterpolator<T> _interpolator = Interpolator.GetDefault<T>()
		?? throw new Exception( $"Can't interpolate type {typeof( T )}." );

	public PropertyBlock<T> From { get; }
	public PropertyBlock<T> To { get; }

	public PropertyBlockInterpolator( PropertyBlock<T> from, PropertyBlock<T> to )
		: base( from.TimeRange )
	{
		if ( from.Equals( to ) )
		{
			throw new ArgumentException( "From and To blocks must be different.",
				nameof( to ) );
		}

		if ( from.TimeRange != to.TimeRange )
		{
			throw new ArgumentException( "From and To blocks must exactly overlap.",
				nameof( to ) );
		}

		From = from;
		To = to;
	}

	protected override T OnGetValue( MovieTime time )
	{
		var alpha = GetAlpha( time );

		return alpha switch
		{
			<= 0 => From.GetValue( time ),
			>= 1 => To.GetValue( time ),
			_ => _interpolator.Interpolate( From.GetValue( time ), To.GetValue( time ), alpha )
		};
	}

	protected abstract float GetAlpha( MovieTime time );

	protected override IEnumerable<MovieTime> OnGetPaintHintTimes( MovieTimeRange timeRange )
	{
		return From.GetPaintHintTimes( timeRange )
			.Merge( To.GetPaintHintTimes( timeRange ) );
	}
}

[JsonDiscriminator( "Blend" )]
file sealed class PropertyBlockBlend<T> : PropertyBlockInterpolator<T>
{
	public float Alpha { get; }

	public PropertyBlockBlend( PropertyBlock<T> from, PropertyBlock<T> to, float alpha )
		: base( from, to )
	{
		Alpha = alpha;
	}

	protected override float GetAlpha( MovieTime time ) => Alpha;

	protected override PropertyBlock<T> OnSlice( MovieTimeRange timeRange )
	{
		return From.Slice( timeRange ).Blend( To.Slice( timeRange ), Alpha );
	}

	protected override PropertyBlock<T> OnShift( MovieTime offset )
	{
		return From.Shift( offset ).Blend( To.Shift( offset ), Alpha );
	}

	protected override int OnGetHashCode()
	{
		return HashCode.Combine( From, To, Alpha );
	}

	protected override bool EqualsBlock( PropertyBlock<T> other )
	{
		return other is PropertyBlockBlend<T> blend
			&& From.Equals( blend.From )
			&& To.Equals( blend.To )
			&& Alpha.Equals( blend.Alpha );
	}
}

[JsonDiscriminator( "CrossFade" )]
file sealed class PropertyBlockCrossFade<T> : PropertyBlockInterpolator<T>
{
	public InterpolationMode Mode { get; }
	public FadeDirection Direction { get; }
	public MovieTimeRange FadeTimeRange { get; }

	public PropertyBlockCrossFade( PropertyBlock<T> from, PropertyBlock<T> to,
		MovieTimeRange fadeTimeRange, InterpolationMode mode, FadeDirection direction )
		: base( from, to )
	{
		if ( mode == InterpolationMode.None )
		{
			throw new ArgumentException( $"Interpolation mode must not be {InterpolationMode.None}.",
				nameof( mode ) );
		}

		if ( !fadeTimeRange.Contains( from.TimeRange ) )
		{
			throw new ArgumentException( "Fade time range needs to contain the faded blocks' time range.",
				nameof( fadeTimeRange ) );
		}

		FadeTimeRange = fadeTimeRange;
		Mode = mode;
		Direction = direction;
	}

	protected override float GetAlpha( MovieTime time )
	{
		var fraction = FadeTimeRange.GetFraction( time );
		return Direction == FadeDirection.FadeIn
			? Mode.Apply( fraction )
			: 1f - Mode.Apply( 1f - fraction );
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
		if ( Mode == InterpolationMode.Linear )
		{
			return base.OnGetPaintHintTimes( timeRange );
		}

		return base.OnGetPaintHintTimes( timeRange )
			.Merge( IPropertyBlock.GetSampleTimes( TimeRange, TimeRange.Start, int.MaxValue, 30 ) );
	}

	protected override int OnGetHashCode()
	{
		return HashCode.Combine( From, To, FadeTimeRange, Mode, Direction );
	}

	protected override bool EqualsBlock( PropertyBlock<T> other )
	{
		return other is PropertyBlockCrossFade<T> fade
			&& From.Equals( fade.From )
			&& To.Equals( fade.To )
			&& FadeTimeRange.Equals( fade.FadeTimeRange )
			&& Mode.Equals( fade.Mode )
			&& Direction.Equals( fade.Direction );
	}
}
