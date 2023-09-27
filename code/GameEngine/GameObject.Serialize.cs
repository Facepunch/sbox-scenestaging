using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed partial class GameObject
{
	//
	// For flexibility purposes, we serialize the GameObject manually
	// into a JsonObject. I haven't benchmarked this, but I assume it's okay.
	//

	public JsonObject Serialize()
	{
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

			Sandbox.Utility.Parallel.ForEach( Components, component =>
			{
				try
				{
					var result = component.Serialize();
					if ( result is null ) return;

					lock ( components )
					{
						components.Add( result );
					}
				}
				catch ( System.Exception e )
				{
					Log.Warning( e, $"Exception when serializing Component" );
				}
			} );

			json.Add( "Components", components );
		}

		if ( Children.Any() )
		{
			var children = new JsonArray();

			Sandbox.Utility.Parallel.ForEach( Children, child =>
			{
				try
				{
					var result = child.Serialize();

					lock ( children )
					{
						children.Add( result );
					}
				}
				catch ( System.Exception e )
				{
					Log.Warning( e, $"Exception when serializing GameObject" );
				}
			} );

			json.Add( "Children", children );
		}

		return json;
	}

	public void Deserialize( JsonObject node )
	{
		Id = node["Id"].Deserialize<Guid>();
		Name = node["Name"].ToString() ?? Name;
		Enabled = (bool)(node["Enabled"] ?? Enabled);
		_transform.Position = node["Position"].Deserialize<Vector3>();
		_transform.Rotation = node["Rotation"].Deserialize<Rotation>();
		_transform.Scale = node["Scale"].Deserialize<Vector3>().x;

		if ( node["Children"] is JsonArray childArray )
		{
			Sandbox.Utility.Parallel.ForEach( childArray, child =>
			{
				if ( child is not JsonObject jso )
					return;

				var go = new GameObject();
				go.Deserialize( jso );

				lock ( this )
				{
					go.Parent = this;
				}

			} );
		}

		if ( node["Components"] is JsonArray componentArray )
		{
			Sandbox.Utility.Parallel.ForEach( componentArray, component =>
			{
				if ( component is not JsonObject jso )
					return;

				var componentType = TypeLibrary.GetType( (string)jso["__type"] );
				
				lock ( this )
				{
					var c = this.AddComponent( componentType );
					c.Deserialize( jso );
				}

			} );
		}
	}
}
