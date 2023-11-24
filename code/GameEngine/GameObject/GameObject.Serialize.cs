using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public partial class GameObject
{
	public class SerializeOptions
	{
		/// <summary>
		/// If we're serializing for network, we won't include any networked objects
		/// </summary>
		public bool SceneForNetwork { get; set; }
	}

	//
	// For flexibility purposes, we serialize the GameObject manually
	// into a JsonObject. I haven't benchmarked this, but I assume it's okay.
	//

	public virtual JsonObject Serialize( SerializeOptions options = null )
	{
		if ( Flags.HasFlag( GameObjectFlags.NotSaved ) )
			return null;

		if ( options is not null )
		{
			if ( options.SceneForNetwork && Network.Active ) return null;
		}

		bool isPartOfPrefab = IsPrefabInstance;

		var json = new JsonObject
		{
			{ "Id", Id },
			{ "Name", Name },
		};
		
		if ( Transform.Position != Vector3.Zero ) json.Add( "Position", JsonValue.Create( Transform.LocalPosition ) );
		if ( Transform.LocalRotation != Rotation.Identity ) json.Add( "Rotation", JsonValue.Create( Transform.LocalRotation ) );
		if ( Transform.LocalScale != 1.0f ) json.Add( "Scale", JsonValue.Create( Transform.LocalScale ) );
		if ( Tags.TryGetAll().Any() ) json.Add( "Tags", string.Join( ",", Tags.TryGetAll() ) );

		if ( Networked ) json.Add( "Networked", true );
		if ( Enabled ) json.Add( "Enabled", true );
		
		
		

		if ( IsPrefabInstanceRoot )
		{
			json.Add( "__Prefab", JsonValue.Create( PrefabSource ) );

			// prefabs don't save their children
			return json;
		}

		if ( Components.Any() && !isPartOfPrefab )
		{
			var components = new JsonArray();

			foreach ( var component in Components )
			{
				if ( component is null ) continue;

				try
				{

					var result = component.Serialize( options );
					if ( result is null ) continue;

					components.Add( result );
				}
				catch ( System.Exception e )
				{
					Log.Warning( e, $"Exception when serializing {component} - skipping!" );
				}
			}

			json.Add( "Components", components );
		}

		if ( Children.Any() )
		{
			var children = new JsonArray();

			foreach ( var child in Children )
			{
				if ( child is null )
					continue;

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

	public virtual void Deserialize( JsonObject node )
	{
		Id = node["Id"].Deserialize<Guid>();
		Name = node["Name"].ToString() ?? Name;
		Transform.LocalPosition = node["Position"]?.Deserialize<Vector3>() ?? Vector3.Zero;
		Transform.LocalRotation = node["Rotation"]?.Deserialize<Rotation>() ?? Rotation.Identity;
		Transform.LocalScale = node["Scale"]?.Deserialize<Vector3>() ?? Vector3.One;

		if ( node["Tags"].Deserialize<string>() is string tags )
		{
			Tags.RemoveAll();
			Tags.Add( tags.Split( ',', StringSplitOptions.RemoveEmptyEntries ) );
		}

		if ( node["__Prefab"].Deserialize<string>() is string prefabSource )
		{
			SetPrefabSource( prefabSource );

			var prefabFile = ResourceLibrary.Get<PrefabFile>( prefabSource );
			if ( prefabFile is null )
			{
				Log.Warning( $"Unable to load prefab '{prefabSource}'" );
				return;
			}

			node = prefabFile.RootObject.Deserialize<JsonObject>(); // make a copy
			SceneUtility.MakeGameObjectsUnique( node ); // change all the guids
		}

		if ( node["Children"] is JsonArray childArray )
		{
			foreach ( var child in childArray )
			{
				if ( child is not JsonObject jso )
					continue;

				var go = new GameObject();

				go.Parent = this;

				go.Deserialize( jso );
			}
		}

		if ( node["Components"] is JsonArray componentArray )
		{
			foreach ( var component in componentArray )
			{
				if ( component is not JsonObject jso )
				{
					Log.Warning( $"Component entry is not an object!" );
					continue;
				}

				var componentType = TypeLibrary.GetType<BaseComponent>( (string)jso["__type"] );
				if ( componentType is null )
				{
					Log.Warning( $"TypeLibrary couldn't find BaseComponent type {jso["__type"]}" );
					continue;
				}

				var c = this.AddComponent( componentType );
				if ( c is null ) continue;

				c.Deserialize( jso );
			}
		}

		Enabled = (bool)(node["Enabled"] ?? false);
		Networked = (bool) (node["Networked"] ?? false);

		ForEachComponent( "OnValidate", false, c => c.OnValidateInternal() );


		if ( !SceneUtility.IsSpawning )
		{
			PostDeserialize();
		}
	}

	public PrefabFile GetAsPrefab()
	{
		var a = new PrefabFile();
		a.RootObject = Serialize();
		return a;
	}

	internal void PostDeserialize()
	{
		foreach ( var component in Components )
		{
			component.PostDeserialize();
		}

		foreach ( var child in Children )
		{
			child.PostDeserialize();
		}
	}
}
