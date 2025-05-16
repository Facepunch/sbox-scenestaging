using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

/// <summary>
/// Describes a selected region of the timeline, including fade in / out.
/// </summary>
public readonly record struct TimeSelection( MovieTimeRange PeakTimeRange, TimeSelection.Fade FadeIn, TimeSelection.Fade FadeOut )
{
	public readonly record struct Fade( MovieTime Duration, InterpolationMode Interpolation );

	public static TimeSelection operator +( TimeSelection selection, MovieTime offset ) =>
		selection with { PeakTimeRange = selection.PeakTimeRange + offset };
	public static TimeSelection operator -( TimeSelection selection, MovieTime offset ) =>
		selection with { PeakTimeRange = selection.PeakTimeRange - offset };

	public TimeSelection( MovieTimeRange peakTimeRange, InterpolationMode interpolation )
		: this( peakTimeRange, new Fade( MovieTime.Zero, interpolation ), new Fade( MovieTime.Zero, interpolation ) )
	{

	}

	public TimeSelection( MovieTimeRange peakTimeRange, MovieTime fadeDuration,
		InterpolationMode interpolationMode = InterpolationMode.Linear )
		: this( peakTimeRange,
			new Fade( fadeDuration, interpolationMode ),
			new Fade( fadeDuration, interpolationMode ) )
	{

	}

	public MovieTime PeakStart => PeakTimeRange.Start;
	public MovieTime PeakEnd => PeakTimeRange.End;
	public MovieTime TotalStart => PeakStart - FadeIn.Duration;
	public MovieTime TotalEnd => PeakEnd + FadeOut.Duration;

	public MovieTimeRange TotalTimeRange => (TotalStart, TotalEnd);
	public MovieTimeRange FadeInTimeRange => (TotalStart, PeakStart);
	public MovieTimeRange FadeOutTimeRange => (PeakEnd, TotalEnd);

	public TimeSelection Clamp( MovieTime maxTime ) => Clamp( (MovieTime.Zero, maxTime) );

	public TimeSelection Clamp( MovieTimeRange timeRange )
	{
		var peakRange = PeakTimeRange.Clamp( timeRange );
		var totalRange = TotalTimeRange.Clamp( timeRange );

		return new TimeSelection( peakRange,
			FadeIn with { Duration = peakRange.Start - totalRange.Start },
			FadeOut with { Duration = totalRange.End - peakRange.End } );
	}

	public TimeSelection WithInterpolation( InterpolationMode interpolation )
	{
		return this with
		{
			FadeIn = FadeIn with { Interpolation = interpolation },
			FadeOut = FadeOut with { Interpolation = interpolation }
		};
	}

	public TimeSelection WithFadeDurationDelta( MovieTime delta )
	{
		return new TimeSelection(
			PeakTimeRange,
			FadeIn with { Duration = MovieTime.Max( MovieTime.Zero, FadeIn.Duration + delta ) },
			FadeOut with { Duration = MovieTime.Max( MovieTime.Zero, FadeOut.Duration + delta ) } );
	}

	public TimeSelection WithTimes( MovieTime? totalStart = null, MovieTime? peakStart = null, MovieTime? peakEnd = null, MovieTime? totalEnd = null )
	{
		var peakRange = new MovieTimeRange( peakStart ?? PeakStart, peakEnd ?? PeakEnd );
		var totalRange = new MovieTimeRange( totalStart ?? TotalStart, totalEnd ?? TotalEnd );

		return new TimeSelection( peakRange,
			FadeIn with { Duration = MovieTime.Max( peakRange.Start - totalRange.Start, MovieTime.Zero ) },
			FadeOut with { Duration = MovieTime.Max( totalRange.End - peakRange.End, MovieTime.Zero ) } );
	}

	public float GetFadeValue( MovieTime time )
	{
		if ( time < PeakStart )
		{
			return FadeIn.Interpolation.Apply( FadeInTimeRange.GetFraction( time ) );
		}

		if ( time > PeakEnd )
		{
			return FadeOut.Interpolation.Apply( 1f - FadeOutTimeRange.GetFraction( time ) );
		}

		return 1f;
	}
}
