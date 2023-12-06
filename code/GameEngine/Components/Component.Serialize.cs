using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Sandbox.NavigationMesh;

public abstract partial class Component
{
	public JsonNode Serialize( GameObject.SerializeOptions options = null )
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

		foreach ( var prop in t.Properties.Where( x => x.HasAttribute<PropertyAttribute>() ).OrderBy( x => x.Name ) )
		{
			var value = prop.GetValue( this );

			try
			{
				json.Add( prop.Name, Json.ToNode( value ) );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e );
			}
		}

		return json;
	}

	JsonObject jsonData;

	public void Deserialize( JsonObject node )
	{
		jsonData = node;
	}

	internal void PostDeserialize()
	{
		if ( jsonData is null )
			return;

		try
		{
			var t = TypeLibrary.GetType( GetType() );

			foreach ( var prop in t.Properties.Where( x => x.HasAttribute<PropertyAttribute>() ).OrderBy( x => x.Name ) )
			{
				var v = jsonData[prop.Name];
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

			InitializeComponent();
			Enabled = (bool)(jsonData["__enabled"] ?? true);
		}
		finally
		{
			jsonData = null;
		}
	}

	/// <summary>
	/// Deserialize this component as per <see cref="Deserialize"/> but update <see cref="GameObject"/> and <see cref="Component"/> property
	/// references immediately instead of having them deferred.
	/// </summary>
	public void DeserializeImmediately( JsonObject node )
	{
		Deserialize( node );
		PostDeserialize();
	}

	private void DeserializeProperty( PropertyDescription prop, JsonNode node )
	{
		prop.SetValue( this, Json.FromNode( node, prop.PropertyType ) );
	}

	public static object JsonRead( ref Utf8JsonReader reader, Type targetType )
	{
		if ( reader.TryGetGuid( out Guid guid ) )
		{
			var go = GameManager.ActiveScene.Directory.FindByGuid( guid );
			if ( go is null ) Log.Warning( $"GameObject {guid} was not found" );
			var component = go.Components.Get( targetType, FindMode.EverythingInSelf );
			if ( component is null ) Log.Warning( $"Component - Unable to find Component on {go}" );

			return component;
		}

		return null;
	}

	public static void JsonWrite( object value, Utf8JsonWriter writer )
	{
		if ( value is not Component component )
			throw new NotImplementedException();

		// components really need a guid, then we'll write an object with gameobject and guid
		writer.WriteStringValue( component.GameObject.Id );

		
	}
}
