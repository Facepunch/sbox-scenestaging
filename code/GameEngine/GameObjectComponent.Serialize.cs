using Sandbox;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using static GameObject;

public abstract partial class GameObjectComponent
{ 
	public JsonNode Serialize( SerializeOptions options = null )
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
			var value = prop.GetValue( this );

			if ( prop.PropertyType == typeof(GameObject) )
			{
				if ( value is GameObject go )
				{
					// todo: if this is a prefab, not in the scene, we need to handle that too!
					json.Add( prop.Name, go.Id );
				}
				continue;
			}

			json.Add( prop.Name, Json.ToNode( value ) );
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

			try
			{
				DeserializeProperty( prop, v );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"Error when deserializing {this}.{prop.Name} ({e.Message})" );
			}
		}

		Enabled = (bool) (node["__enabled"] ?? true);
	}

	private void DeserializeProperty( PropertyDescription prop, JsonNode node )
	{
		if ( prop.PropertyType == typeof( GameObject ) )
		{
			string guidString = node.Deserialize<string>();

			if ( Guid.TryParse( guidString, out Guid guid ) )
			{
				onAwake += () => prop.SetValue( this, Scene.FindObjectByGuid( guid ) );
				return;
			}

			throw new System.Exception( $"Couldn't parse '{guidString}' as object guid" );
		}

		prop.SetValue( this, Json.FromNode( node, prop.PropertyType ) );
	}
}
