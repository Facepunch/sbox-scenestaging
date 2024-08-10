using Sandbox.MovieMaker.Tracks;
namespace Sandbox.MovieMaker;

public partial class MovieClip : IJsonConvert
{
	public List<MovieTrack> Tracks { get; set; } = new List<MovieTrack>();

	float _time;

	public void ScrubTo( float time )
	{
		_time = time;

		foreach ( var track in Tracks )
		{
			track.Play( _time );
		}
	}

	public void Play( float delta )
	{
		ScrubTo( _time + delta );
	}

	public MovieTrack FindTrack( GameObject go, string property )
	{
		return Tracks.OfType<PropertyTrack>().Where( x => x.Matches( go, property ) ).FirstOrDefault();
	}

	public MovieTrack FindOrCreateTrack( GameObject go, string property )
	{
		return FindTrack( go, property ) ?? AddTrack( go, property );
	}

	public MovieTrack FindTrack( Component component, string property )
	{
		return Tracks.OfType<PropertyTrack>().Where( x => x.Matches( component, property ) ).FirstOrDefault();
	}

	public MovieTrack FindOrCreateTrack( Component component, string property )
	{
		return FindTrack( component, property ) ?? AddTrack( component, property );
	}

	private MovieTrack AddTrack( GameObject go, string property )
	{
		var track = PropertyTrack.CreateFor( go, property );
		track.InitProperty();

		Tracks.Add( track );

		Log.Info( $"Adding track for {go}{property} = {Tracks.Count}" );
		return track;
	}

	private MovieTrack AddTrack( Component component, string property )
	{
		var track = PropertyTrack.CreateFor( component, property );
		track.InitProperty();

		Tracks.Add( track );

		Log.Info( $"Adding track for {component}{property} = {Tracks.Count}" );
		return track;
	}

	public void RemoveTrack( MovieTrack source )
	{
		Tracks.Remove( source );
	}
}
