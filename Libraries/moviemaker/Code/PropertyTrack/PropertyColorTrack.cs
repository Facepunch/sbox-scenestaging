namespace Sandbox.MovieMaker.Tracks;

public class PropertyColorTrack : PropertyElementalTrack
{
	public override int ElementCount => 4;

	public override void CreateTracks()
	{
		InitTrack( 0, "R" );
		InitTrack( 1, "G" );
		InitTrack( 2, "B" );
		InitTrack( 3, "A" );
	}

	public override float?[] ValueToElements( object value )
	{
		if ( value is Color v )
		{
			return new float?[] { v.r, v.g, v.b, v.a };
		}

		return null;
	}

	public Color Evaluate( float time )
	{
		return new Color( Evaluate( 0, time ), Evaluate( 1, time ), Evaluate( 2, time ), Evaluate( 3, time ) );
	}

	public override void Play( float time )
	{
		WriteValue?.Invoke( Evaluate( time ) );
	}
}
