namespace Sandbox.MovieMaker.Tracks;

public class PropertyVector3Track : PropertyElementalTrack
{
	public override int ElementCount => 3;

	public override void CreateTracks()
	{
		InitTrack( 0, "X" );
		InitTrack( 1, "Y" );
		InitTrack( 2, "Z" );
	}

	public override float?[] ValueToElements( object value )
	{
		if ( value is Vector3 v )
		{
			return new float?[] { v.x, v.y, v.z };
		}

		return null;
	}

	public Vector3 Evaluate( float time )
	{
		return new Vector3( Evaluate( 0, time ), Evaluate( 1, time ), Evaluate( 2, time ) );
	}

	public override void Play( float time )
	{
		WriteValue?.Invoke( Evaluate( time ) );
	}
}
