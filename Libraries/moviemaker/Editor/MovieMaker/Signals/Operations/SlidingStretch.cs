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
	}

	public MovieTransform GetTransformAt( MovieTime time )
	{
		throw new NotImplementedException();
	}
}
