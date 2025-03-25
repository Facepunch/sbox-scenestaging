using System.Text;
using Sandbox.MovieMaker;

namespace Editor.MovieMaker;

#nullable enable

public readonly struct MovieTimeScale : IEquatable<MovieTimeScale>
{
	public int Cents { get; }

	public double DurationScale => Inverse.FrequencyScale;
	public double FrequencyScale => Cents == 0 ? 1d : Math.Pow( 2d, Cents / 1200d );

	public MovieTimeScale Inverse => FromCents( -Cents );

	private MovieTimeScale( int cents )
	{
		Cents = cents;
	}

	public bool Equals( MovieTimeScale other ) => Cents == other.Cents;
	public override bool Equals( object? obj ) => obj is MovieTimeScale other && Equals( other );
	public override int GetHashCode() => Cents;

	public static MovieTimeScale FromCents( int cents ) => new( cents );
	public static MovieTimeScale FromDurationScale( double scale ) => FromFrequencyScale( scale ).Inverse;
	public static MovieTimeScale FromFrequencyScale( double scale ) => new( (int)Math.Round( Math.Log2( scale ) * 1200 ) );

	public static MovieTimeScale FromDurationChange( MovieTime oldDuration, MovieTime newDuration ) =>
		FromDurationScale( newDuration.TotalSeconds / oldDuration.TotalSeconds );

	public static MovieTimeScale Identity => default;

	public static bool operator ==( MovieTimeScale a, MovieTimeScale b ) => a.Cents == b.Cents;
	public static bool operator !=( MovieTimeScale a, MovieTimeScale b ) => a.Cents != b.Cents;

	public static MovieTime operator *( MovieTime time, MovieTimeScale timeScale ) =>
		timeScale == Identity ? time : MovieTime.FromSeconds( time.TotalSeconds * timeScale.DurationScale );
	public static MovieTime operator *( MovieTimeScale timeScale, MovieTime time ) =>
		timeScale == Identity ? time : MovieTime.FromSeconds( time.TotalSeconds * timeScale.DurationScale );

	public static MovieTime operator /( MovieTime time, MovieTimeScale timeScale ) =>
		timeScale == Identity ? time : MovieTime.FromSeconds( time.TotalSeconds * timeScale.FrequencyScale );

	public static MovieTimeScale operator *( MovieTimeScale a, MovieTimeScale b ) => FromCents( a.Cents + b.Cents );
	public static MovieTimeScale operator /( MovieTimeScale a, MovieTimeScale b ) => FromCents( a.Cents - b.Cents );

	public override string ToString()
	{
		return this == Identity
			? $"{nameof(MovieTimeScale)} {{ {nameof(Identity)} }}"
			: $"{nameof(MovieTimeScale)} {{ {nameof(Cents)} = {Cents} }}";
	}
}

/// <summary>
/// Describes a translation and scale that can be applied to <see cref="MovieTime"/>s.
/// </summary>
/// <param name="Translation">Time offset to apply.</param>
/// <param name="Scale">Time scale to apply.</param>
public readonly record struct MovieTransform( MovieTime Translation = default, MovieTimeScale Scale = default )
{
	public static MovieTransform Identity => default;

	public MovieTransform Inverse => new( Scale.Inverse * -Translation, Scale.Inverse );

	public static MovieTime operator *( MovieTransform transform, MovieTime time ) =>
		time * transform.Scale + transform.Translation;

	public static MovieTimeRange operator *( MovieTransform transform, MovieTimeRange timeRange ) =>
		(transform * timeRange.Start, transform * timeRange.End);

	public static MovieTransform operator *( MovieTransform a, MovieTransform b ) =>
		new( a * b.Translation, a.Scale * b.Scale );
	public static MovieTransform operator *( MovieTimeScale a, MovieTransform b ) =>
		new( a * b.Translation, a * b.Scale );

	public static MovieTransform operator +( MovieTransform transform, MovieTime translation ) =>
		transform with { Translation = transform.Translation + translation };

	public static MovieTransform FromTo( MovieTimeRange from, MovieTimeRange to )
	{
		var scale = MovieTimeScale.FromDurationChange( from.Duration, to.Duration );

		return scale * new MovieTransform( -from.Start ) + to.Start;
	}

	private bool PrintMembers( StringBuilder builder )
	{
		if ( this == Identity )
		{
			builder.Append( "Identity" );
			return true;
		}

		if ( !Translation.IsZero )
		{
			builder.Append( $"{nameof(Translation)} = {Translation}" );
		}

		if ( Scale != MovieTimeScale.Identity )
		{
			if ( !Translation.IsZero )
			{
				builder.Append( ", " );
			}

			builder.Append( $"{nameof(Scale)} = {Scale}" );
		}

		return true;
	}
}
