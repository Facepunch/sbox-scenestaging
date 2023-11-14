using Sandbox;
using Sandbox.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;

public partial class Scene : GameObject
{
	public virtual void Load( GameResource resource )
	{
		Assert.NotNull( resource );

		ProcessDeletes();
		Clear();

		if ( resource is SceneFile sceneFile )
		{
			Source = sceneFile;
			Title = sceneFile.Title ?? sceneFile.ResourceName;
			Description = sceneFile.Description ?? "";

			using var sceneScope = Push();

			using var spawnScope = SceneUtility.DeferInitializationScope( "Load" );

			if ( sceneFile.GameObjects is not null )
			{
				foreach ( var json in sceneFile.GameObjects )
				{
					var go = CreateObject( false );
					go.Deserialize( json );
				}
			}

			if ( sceneFile.SceneProperties is not null )
			{
				DeserializeProperties( sceneFile.SceneProperties );
			}
		}
	}

	public void LoadFromFile( string filename )
	{
		var file = ResourceLibrary.Get<SceneFile>( filename );
		if ( file is null )
		{
			Log.Warning( $"LoadFromFile: Couldn't find {filename}" );
			return;
		}

		Load( file );
	}

	public virtual GameResource Save()
	{
		var a = new SceneFile();
		a.GameObjects = Children.Select( x => x.Serialize() ).ToArray();
		a.SceneProperties = SerializeProperties();
		a.Title = Title;
		a.Description = Description;
		return a;
	}

	JsonObject SerializeProperties()
	{

		var jso = new JsonObject();
		foreach ( var prop in TypeLibrary.GetType<Scene>().Properties.Where( x => x.HasAttribute<PropertyAttribute>() ) )
		{
			if ( prop.Name == "Enabled" ) continue;
			if ( prop.Name == "Name" ) continue;

			jso.Add( prop.Name, JsonValue.Create( prop.GetValue( this ) ) );
		}

		return jso;
	}

	void DeserializeProperties( JsonObject data )
	{
		foreach ( var prop in TypeLibrary.GetType<Scene>().Properties.Where( x => x.HasAttribute<PropertyAttribute>() ) )
		{
			if ( prop.Name == "Enabled" ) continue;
			if ( prop.Name == "Name" ) continue;

			if ( !data.TryGetPropertyValue( prop.Name, out JsonNode node ) )
				continue;

			try
			{
				prop.SetValue( this, Json.FromNode( node, prop.PropertyType ) );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"Error when deserializing {this}.{prop.Name} ({e.Message})" );
			}
		}


	}
}
