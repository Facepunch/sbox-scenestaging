using Sandbox.MovieMaker;
using System.Text.Json.Serialization;

namespace Editor.MovieMaker;

#nullable enable

public enum FadeDirection
{
	FadeIn,
	FadeOut
}

[JsonDiscriminator( "CrossFade" )]
file sealed record CrossFadeOperation<T>( PropertySignal<T> First, PropertySignal<T> Second, MovieTimeRange FadeTimeRange,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] InterpolationMode Mode,
	[property: JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingDefault )] FadeDirection Direction ) : InterpolateOperation<T>( First, Second )
{
	public override float GetAlpha( MovieTime time ) => Direction == FadeDirection.FadeOut
		? 1f - Mode.Apply( 1f - FadeTimeRange.GetFraction( time ) )
		: Mode.Apply( FadeTimeRange.GetFraction( time ) );

	protected override PropertySignal<T> OnTransform( MovieTime offset ) => this with
	{
		First = First.Transform( offset ),
		Second = Second.Transform( offset ),
		FadeTimeRange = FadeTimeRange + offset
	};

	protected override PropertySignal<T> OnReduce( MovieTime? start, MovieTime? end )
	{
		return !TryReduceTransition( start, end, FadeTimeRange, out var reduced,
			out var before, out var after )
			? before.CrossFade( after, FadeTimeRange, Mode, Direction )
			: reduced;
	}

	public override IEnumerable<MovieTimeRange> GetPaintHints( MovieTimeRange timeRange )
	{
		var hints = GetTransitionPaintHints( timeRange, FadeTimeRange );

		return timeRange.Intersect( FadeTimeRange ) is { } intersection
			? hints.Union( [intersection] )
			: hints;
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
