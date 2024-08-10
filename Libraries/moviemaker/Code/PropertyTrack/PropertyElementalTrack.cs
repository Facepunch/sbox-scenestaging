using Sandbox.Diagnostics;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker.Tracks;

public abstract class PropertyElementalTrack : PropertyTrack
{
	// TODO - in the UI we can allow unfolding to allow editing the tracks independently
	PropertyFloatTrack[] Tracks;

	public abstract int ElementCount { get; }
	public abstract float?[] ValueToElements( object value );
	public abstract void CreateTracks();

	public PropertyElementalTrack()
	{
		Tracks = new PropertyFloatTrack[ElementCount];
		CreateTracks();
	}

	protected void InitTrack( int i, string name )
	{
		Tracks[i] = new PropertyFloatTrack();
		Tracks[i].SetPropertyName( name );
	}

	public float Evaluate( int element, float time )
	{
		return Tracks[element].Evaluate( time );
	}

	protected override JsonObject Serialize()
	{
		var o = base.Serialize();

		var a = new JsonArray();

		for ( int i = 0; i < ElementCount; i++ )
		{
			a.Add( Json.ToNode( Tracks[i].Curve ) );
		}

		o["Elements"] = a;

		return o;
	}

	protected override void Deserialize( JsonObject obj )
	{
		base.Deserialize( obj );

		if ( obj.TryGetJsonArray( "Elements", out var array ) )
		{
			for ( int i = 0; i < ElementCount; i++ )
			{
				Tracks[i].Curve = Json.FromNode<Curve>( array[i] );
			}
		}

		Recalc();
	}

	private void Recalc()
	{
		Duration = Tracks.Max( x => x.Curve.Frames.Max( x => x.Time ) );
	}

	public override PropertyKeyframe[] ReadFrames()
	{
		Dictionary<float, PropertyKeyframe> groupFrames = new();

		for ( int i = 0; i < ElementCount; i++ )
		{
			foreach ( var frame in Tracks[i].ReadFrames() )
			{
				groupFrames.TryGetValue( frame.time, out var keyframe );
				keyframe.value ??= new float?[ElementCount];
				keyframe.time = frame.time;

				if ( keyframe.value is float?[] array )
				{
					array[i] = (float)frame.value;
				}

				groupFrames[frame.time] = keyframe;
			}
		}

		return groupFrames.Values.OrderBy( x => x.time ).ToArray();
	}

	public override void WriteFrames( PropertyKeyframe[] frames )
	{
		for ( int i = 0; i < ElementCount; i++ )
		{
			Tracks[i].Curve = new Curve();
		}

		foreach ( var f in frames )
		{
			if ( f.value is not float?[] values )
				continue;

			for ( int i = 0; i < ElementCount; i++ )
			{
				if ( values[i].HasValue )
				{
					Tracks[i].Curve.AddPoint( f.time, values[i].Value );
				}
			}

		}

		Recalc();
	}

	/// <summary>
	/// Needs to return a float?[] to be compatible with ReadFrames/WriteFrames
	/// </summary>
	public override object ReadCurrentValue()
	{
		var value = ReadValue?.Invoke();
		if ( value is null ) return new float?[ElementCount];

		var e = ValueToElements( value );
		if ( e is null ) return new float?[ElementCount];

		Assert.True( e.Length == ElementCount );

		return e;
	}
}
