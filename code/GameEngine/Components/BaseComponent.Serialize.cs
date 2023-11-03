using Sandbox;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public abstract partial class BaseComponent
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

			if ( prop.PropertyType == typeof( GameObject ) )
			{
				if ( value is GameObject go )
				{
					if ( go is PrefabScene prefabScene )
					{
						if ( prefabScene.Source is null )
						{
							Log.Warning( "Prefab scene has no source!" );
							continue;
						}

						json.Add( prop.Name, prefabScene.Source.ResourcePath );
					}
					else
					{
						// todo: if this is a prefab, not in the scene, we need to handle that too!
						json.Add( prop.Name, go.Id );
					}
				}
				continue;
			}

			if ( prop.PropertyType.IsAssignableTo( typeof( BaseComponent ) ) )
			{
				if ( value is BaseComponent component )
				{
					json.Add( prop.Name, component.GameObject.Id );
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
			var v = node[prop.Name];
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
		Enabled = (bool)(node["__enabled"] ?? true);
	}

	private void DeserializeProperty( PropertyDescription prop, JsonNode node )
	{
		if ( prop.PropertyType == typeof( GameObject ) )
		{
			string guidString = node.Deserialize<string>();

			if ( Guid.TryParse( guidString, out Guid guid ) )
			{
				onPostDeserialize += () =>
				{
					var go = Scene.Directory.FindByGuid( guid );
					if ( go is null ) Log.Warning( $"GameObject - {guid} was not found for {GetType().Name}.{prop.Name}" );
					prop.SetValue( this, go );
				};
				return;
			}

			if ( ResourceLibrary.TryGet( guidString, out PrefabFile prefabFile ) )
			{
				prop.SetValue( this, prefabFile.Scene );
				return;
			}

			throw new System.Exception( $"Couldn't parse '{guidString}' as object guid" );
		}

		if ( prop.PropertyType.IsAssignableTo( typeof( BaseComponent ) ) )
		{
			string guidString = node.Deserialize<string>();

			if ( Guid.TryParse( guidString, out Guid guid ) )
			{
				onPostDeserialize += () =>
				{
					var go = Scene.Directory.FindByGuid( guid );
					if ( go is null ) Log.Warning( $"GameObject - {guid} was not found for {GetType().Name}.{prop.Name}" );

					var component = go.GetComponent( prop.PropertyType, false, false );
					if ( component is null ) Log.Warning( $"Component - Unable to find {prop.PropertyType} on {go} for {GetType().Name}.{prop.Name}" );

					prop.SetValue( this, component );
				};
				return;
			}

			throw new System.Exception( $"Couldn't parse '{guidString}' as object guid" );
		}

		prop.SetValue( this, Json.FromNode( node, prop.PropertyType ) );
	}
}
