using Sandbox;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public abstract partial class GameObjectComponent
{ 
	public JsonNode Serialize()
	{
		var t = TypeLibrary.GetType( GetType() );
		if ( t is null )
		{
			Log.Warning( $"TypeLibrary could not find {GetType()}" );
			return null;
		}

		var json = new JsonObject
		{
			{ "__type", t.ClassName },
			{ "__enabled", Enabled },
		};

		foreach( var prop in t.Properties.Where( x => x.HasAttribute<PropertyAttribute>() ).OrderBy( x => x.Name ) )
		{
			json.Add( prop.Name, Json.ToNode( prop.GetValue( this ) ) );
		}

		return json;
	}

	public void Deserialize( JsonObject node )
	{
		var t = TypeLibrary.GetType( GetType() );

		foreach ( var prop in t.Properties.Where( x => x.HasAttribute<PropertyAttribute>() ).OrderBy( x => x.Name ) )
		{
			var v = node[ prop.Name ];
			if ( v is null ) continue;

			prop.SetValue( this, Json.FromNode( v, prop.PropertyType ) );
		}

		Enabled = (bool) (node["__enabled"] ?? true);
	}
}
