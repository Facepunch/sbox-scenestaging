using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

partial record PropertySignal<T>
{
	public PropertySignal<T> SlidingStretch( MovieTimeScale timeScale, MovieTimeRange slideTimeRange )
	{
		return timeScale != MovieTimeScale.Identity ? new SlidingStretchOperation<T>( this, timeScale, slideTimeRange ) : this;
	}

	public PropertySignal<T> SlidingStretch( MovieTime sourceDuration, TimeSelection envelope )
	{
		var (slideIn, slideOut) = SlidingStretchTransform.FromEnvelope( sourceDuration, envelope );

		return SlidingStretch( slideIn.TimeScale, slideIn.SlideTimeRange )
			.SlidingStretch( slideOut.TimeScale, slideOut.SlideTimeRange );
	}
}

[JsonDiscriminator( "SlidingStretch" )]
file sealed record SlidingStretchOperation<T>( PropertySignal<T> Signal,
	MovieTimeScale TimeScale, MovieTimeRange SlideTimeRange )
	: UnaryOperation<T>( Signal )
{
	public override T GetValue( MovieTime time )
	{
		var transform = new SlidingStretchTransform( TimeScale, SlideTimeRange )
			.GetTransformAt( time );

		return Signal.GetValue( transform.Inverse * time );
	}
}

public readonly record struct SlidingStretchTransform( MovieTimeScale TimeScale, MovieTimeRange SlideTimeRange )
{
	public static (SlidingStretchTransform In, SlidingStretchTransform Out) FromEnvelope( MovieTime sourceDuration, TimeSelection envelope )
	{
		throw new NotImplementedException();

		var fadeDuration = envelope.FadeIn.Duration + envelope.FadeOut.Duration;
		var peakTimeScale = MovieTimeScale.FromFrequencyScale( (2 * sourceDuration.TotalSeconds - fadeDuration.TotalSeconds)
			/ (2 * envelope.PeakTimeRange.Duration.TotalSeconds + fadeDuration.TotalSeconds) );

		var slideIn = new SlidingStretchTransform( peakTimeScale, envelope.FadeInTimeRange );
		var slideOut = new SlidingStretchTransform( peakTimeScale.Inverse,
			slideIn.GetTransformAt( envelope.FadeInTimeRange.End ) * envelope.FadeOutTimeRange );

		return (slideIn, slideOut);
	}

	public MovieTransform GetTransformAt( MovieTime time )
	{
		throw new NotImplementedException();

		if ( time <= SlideTimeRange.Start ) return MovieTransform.Identity;

		var avgTimeScale = MovieTimeScale.FromFrequencyScale( 1d + (TimeScale.FrequencyScale - 1d) * 0.5 );
		var slideEndTranslation = avgTimeScale * SlideTimeRange.Duration - SlideTimeRange.Duration;
		var slideProgress = SlideTimeRange.GetFraction( time );
		var scale = MovieTimeScale.FromFrequencyScale( 1d + (TimeScale.FrequencyScale - 1d) * slideProgress );
		var translation = MovieTime.FromSeconds( slideProgress * slideEndTranslation.TotalSeconds );

		return new MovieTransform( SlideTimeRange.Start ) * new MovieTransform( translation, scale ) * new MovieTransform( -SlideTimeRange.Start );
	}
}
