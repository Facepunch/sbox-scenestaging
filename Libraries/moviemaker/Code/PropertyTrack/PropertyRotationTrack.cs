namespace Sandbox.MovieMaker.Tracks;

public class PropertyRotationTrack : PropertyElementalTrack
{
	public override int ElementCount => 3;

	public override void CreateTracks()
	{
		InitTrack( 0, "Pitch" );
		InitTrack( 1, "Yaw" );
		InitTrack( 2, "Roll" );
	}

	public override float?[] ValueToElements( object value )
	{
		if ( value is Rotation rot )
		{
			var angle = rot.Angles();
			return new float?[] { angle.pitch, angle.yaw, angle.roll };
		}

		return null;
	}

	public Angles Evaluate( float time )
	{
		return new Angles( Evaluate( 0, time ), Evaluate( 1, time ), Evaluate( 2, time ) );
	}

	public override void Play( float time )
	{
		WriteValue?.Invoke( (Rotation)Evaluate( time ) );
	}
}
