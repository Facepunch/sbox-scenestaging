using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class GameObject
{
	public class SerializeOptions
	{
	}

	//
	// For flexibility purposes, we serialize the GameObject manually
	// into a JsonObject. I haven't benchmarked this, but I assume it's okay.
	//

	public JsonObject Serialize( SerializeOptions options = null )
	{
		if ( PrefabSource is not null )
			return PrefabSource.RootObject;

		if ( Flags.HasFlag( GameObjectFlags.NotSaved ) )
			return null;

		var json = new JsonObject
		{
			{ "Id", Id },
			{ "Name", Name },
			{ "Enabled", Enabled },
			{ "Position",  JsonValue.Create( Transform.Position.ToString() ) },
			{ "Rotation", JsonValue.Create( Transform.Rotation ) },
			{ "Scale", JsonValue.Create( (Vector3)Transform.Scale ) }
		};

		if ( Components.Any() )
		{
			var components = new JsonArray();

			foreach( var component in Components )
			{
				try
				{
					var result = component.Serialize( options );
					if ( result is null ) continue;

					components.Add( result );
				}
				catch ( System.Exception e )
				{
					Log.Warning( e, $"Exception when serializing Component" );
				}
			}

			json.Add( "Components", components );
		}

		if ( Children.Any() )
		{
			var children = new JsonArray();

			foreach( var child in Children )
			{
				try
				{
					var result = child.Serialize( options );

					if ( result is not null )
					{
						children.Add( result );
					}
				}
				catch ( System.Exception e )
				{
					Log.Warning( e, $"Exception when serializing GameObject" );
				}
			}

			json.Add( "Children", children );
		}

		return json;
	}

	public void Deserialize( JsonObject node )
	{
		Id = node["Id"].Deserialize<Guid>();
		Name = node["Name"].ToString() ?? Name;
		_transform.Position = node["Position"].Deserialize<Vector3>();
		_transform.Rotation = node["Rotation"].Deserialize<Rotation>();
		_transform.Scale = node["Scale"].Deserialize<Vector3>().x;

		if ( node["Children"] is JsonArray childArray )
		{
			foreach( var child in  childArray )
			{
				if ( child is not JsonObject jso )
					return;

				var go = GameObject.Create();
				
				go.Parent = this;

				go.Deserialize( jso );

			}
		}

		if ( node["Components"] is JsonArray componentArray )
		{
			foreach( var component in componentArray )
			{
				if ( component is not JsonObject jso )
					return;

				var componentType = TypeLibrary.GetType( (string)jso["__type"] );
				if ( componentType is null )
					return;
				
				var c = this.AddComponent( componentType );
				if ( c is null ) continue;
				
				c.Deserialize( jso );
			}
		}

		Enabled = (bool)(node["Enabled"] ?? Enabled);
		UpdateEnabledStatus();
	}

	public PrefabFile GetAsPrefab()
	{
		var a = new PrefabFile();
		a.RootObject = Serialize();
		return a;
	}
}
