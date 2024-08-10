using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sandbox.MovieMaker.Tracks;

//
// With this class the value can be anything. When deserializing we store the value as a 
// JsonNode - because we might not know the type yet. When it comes to playing, we conver it
// from a JsonNode to the target type - if we have an appropriate target type.
//
// This means that after deserializing, even if they don't have the gameobject or property
// for some reason, the data won't be lost due to no conversion. It'll still be there as a Json data
// which can be re-saved and stick around until it's needed.
//

public class PropertyGenericTrack : PropertyTrack
{
	SortedDictionary<float, object> values = new();

	public override void Play( float time )
	{
		var value = values.LastOrDefault( x => x.Key < time );
		if ( value.Key == default && value.Value is null ) return;

		var v = value.Value;

		if ( v is JsonNode node )
		{
			if ( PropertyType is null ) return;
			v = Json.FromNode( node, PropertyType );
			values[value.Key] = v;
		}

		WriteValue?.Invoke( v );
	}

	public override void WriteFrames( PropertyKeyframe[] frames )
	{
		values.Clear();

		foreach ( var f in frames )
		{
			values[f.time] = f.value;
		}

		Duration = values.Max( x => x.Key );
	}

	public override PropertyKeyframe[] ReadFrames()
	{
		List<PropertyKeyframe> framees = new List<PropertyKeyframe>();

		foreach ( var f in values )
		{
			framees.Add( new PropertyKeyframe { time = f.Key, value = f.Value } );
		}

		return framees.ToArray();
	}

	protected override JsonObject Serialize()
	{
		var o = base.Serialize();
		o["PropertyType"] = PropertyType?.Name;

		var jsa = new JsonArray();
		foreach ( var val in values )
		{
			jsa.Add( new JsonArray
			{
				val.Key,
				Json.ToNode( val.Value )
			} );
		}

		o["Data"] = jsa;
		return o;
	}

	protected override void Deserialize( JsonObject obj )
	{
		base.Deserialize( obj );

		values = new();

		if ( obj.TryGetJsonArray( "Data", out var data ) )
		{
			foreach ( JsonArray entry in data )
			{
				if ( entry.Count != 2 ) continue;

				var time = entry[0].Deserialize<float>();
				values[time] = entry[1].Deserialize<JsonNode>();
			}
		}

		Duration = values.Max( x => x.Key );


	}
}
