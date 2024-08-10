using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker.Tracks;

public class PropertyFloatTrack : PropertyTrack
{
	public Curve Curve = new Curve();

	public float Evaluate( float time )
	{
		return Curve.Evaluate( time );
	}

	public override void Play( float time )
	{
		WriteValue?.Invoke( Evaluate( time ) );
	}

	public override PropertyKeyframe[] ReadFrames()
	{
		List<PropertyKeyframe> framees = new List<PropertyKeyframe>();

		for ( int i = 0; i < Curve.Length; i++ )
		{
			framees.Add( new PropertyKeyframe { time = Curve[i].Time, value = Curve[i].Value } );
		}

		return framees.ToArray();
	}

	public override void WriteFrames( PropertyKeyframe[] frames )
	{
		Curve.Frames = default;

		foreach ( var f in frames )
		{
			if ( f.value is float v )
			{
				Curve.AddPoint( f.time, v );
			}
		}

		Duration = Curve.Frames.Max( x => x.Time );
	}

	protected override JsonObject Serialize()
	{
		var o = base.Serialize();

		o["F"] = Json.ToNode( Curve );

		return o;
	}

	protected override void Deserialize( JsonObject obj )
	{
		base.Deserialize( obj );

		Curve = obj["F"]?.Deserialize<Curve>() ?? default;

		Duration = Curve.Frames.Max( x => x.Time );
	}

}
