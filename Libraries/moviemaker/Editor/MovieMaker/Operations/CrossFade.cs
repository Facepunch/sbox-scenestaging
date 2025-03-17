using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public enum FadeDirection
{
	FadeIn,
	FadeOut
}

[JsonDiscriminator( "CrossFade" )]
file sealed record CrossFadeOperation<T>( PropertySignal<T> First, PropertySignal<T> Second, MovieTimeRange FadeTimeRange,
	InterpolationMode Mode, FadeDirection Direction ) : InterpolateOperation<T>( First, Second )
{
	public override float GetAlpha( MovieTime time ) => Direction == FadeDirection.FadeOut
		? 1f - Mode.Apply( 1f - FadeTimeRange.GetFraction( time ) )
		: Mode.Apply( FadeTimeRange.GetFraction( time ) );

	protected override PropertySignal<T> OnReduce( MovieTime offset, MovieTime? start, MovieTime? end )
	{
		if ( start >= FadeTimeRange.End + offset ) return Second.Reduce( offset, start, end );
		if ( end <= FadeTimeRange.Start + offset ) return First.Reduce( offset, start, end );

		var first = First.Reduce( offset, start, FadeTimeRange.End + offset );
		var second = Second.Reduce( offset, FadeTimeRange.Start + offset, end );

		if ( offset.IsZero && first.Equals( First ) && second.Equals( Second ) )
		{
			return this;
		}

		return first.CrossFade( second, FadeTimeRange + offset, Mode, Direction );
	}
}

partial class PropertySignalExtensions
{
	public static PropertySignal<T> CrossFade<T>( this PropertySignal<T> first, PropertySignal<T> second,
		MovieTimeRange fadeTimeRange, InterpolationMode mode = InterpolationMode.Linear, FadeDirection direction = FadeDirection.FadeIn )
	{
		if ( first.Equals( second ) )
		{
			return first;
		}

		if ( Interpolator.GetDefault<T>() is null || mode == InterpolationMode.None )
		{
			fadeTimeRange = direction == FadeDirection.FadeIn
				? fadeTimeRange.End
				: fadeTimeRange.Start;
		}

		return !fadeTimeRange.IsEmpty
			? new CrossFadeOperation<T>( first, second, fadeTimeRange, mode, direction )
			: first.HardCut( second, fadeTimeRange.Start );
	}

	public static PropertySignal<T> CrossFade<T>( this PropertySignal<T> first, PropertySignal<T> second,
		TimeSelection envelope )
	{
		// ReSharper disable once RedundantArgumentDefaultValue
		return first
			.CrossFade( second, envelope.FadeInTimeRange, envelope.FadeIn.Interpolation, FadeDirection.FadeIn )
			.CrossFade( first, envelope.FadeOutTimeRange, envelope.FadeOut.Interpolation, FadeDirection.FadeOut );
	}
}
