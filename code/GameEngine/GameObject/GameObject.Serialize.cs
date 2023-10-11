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
		if ( Flags.HasFlag( GameObjectFlags.NotSaved ) )
			return null;

		bool isPartOfPrefab = IsPrefabInstance;

		var json = new JsonObject
		{
			{ "Id", Id },
			{ "Name", Name },
			{ "Enabled", Enabled },
			{ "Position",  JsonValue.Create( Transform.LocalPosition ) },
			{ "Rotation", JsonValue.Create( Transform.LocalRotation ) },
			{ "Scale", JsonValue.Create( Transform.LocalScale ) }
		};

		if ( IsPrefabInstanceRoot )
		{
			json.Add( "__Prefab", JsonValue.Create( PrefabSource ) );

			if ( PrefabLut is not null )
			{
				json.Add( "__PrefabLut", JsonSerializer.SerializeToNode( PrefabLut ) );
			}
		}

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
		Transform.LocalPosition = node["Position"].Deserialize<Vector3>();
		Transform.LocalRotation = node["Rotation"].Deserialize<Rotation>();
		Transform.LocalScale = node["Scale"].Deserialize<Vector3>();

		if ( node["__Prefab"].Deserialize<string>() is string prefabSource )
		{
			SetPrefabSource( prefabSource );
		}

		if ( node["__PrefabLut"] is JsonObject a )
		{
			PrefabLut = a.Deserialize<Dictionary<Guid, Guid>>();
		}

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

		SceneUtility.ActivateGameObject( this );
	}

	public PrefabFile GetAsPrefab()
	{
		var a = new PrefabFile();
		a.RootObject = Serialize();
		return a;
	}
}
