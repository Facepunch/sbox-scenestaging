using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker;

public partial class MovieClip
{
	public float Duration => Tracks.Max( x => x.Duration );

	static void IJsonConvert.JsonWrite( object value, Utf8JsonWriter writer )
	{
		if ( value is MovieClip clip )
		{
			var o = clip.Serialize();
			JsonSerializer.Serialize( writer, o );
		}
	}

	public JsonObject Serialize()
	{
		var o = new JsonObject();

		var tracks = new JsonArray();

		foreach ( var track in Tracks )
		{
			var entry = track.SerializeInternal();
			if ( entry is null ) continue;

			tracks.Add( entry );
		}

		o.Add( "tracks", tracks );

		return o;
	}

	static object IJsonConvert.JsonRead( ref Utf8JsonReader reader, Type typeToConvert )
	{
		var obj = Json.ParseToJsonObject( ref reader );

		MovieClip clip = new();
		clip.Deserialize( obj );
		return clip;
	}

	public void Deserialize( JsonObject o )
	{
		if ( o.TryGetJsonArray( "tracks", out var trackArray ) )
		{
			foreach ( JsonObject track in trackArray )
			{
				if ( !MovieTrack.TryCreate( track, out var t ) )
					continue;

				Tracks.Add( t );
			}
		}
	}
}
