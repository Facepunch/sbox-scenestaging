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

	public override JsonObject Serialize( SerializeOptions options = null )
	{
		var json = new JsonObject
		{
			{ "Type", "Scene" },
		};

		var children = new JsonArray();

		foreach( var child in Children )
		{
			children.Add( child.Serialize() );
		}

		json.Add( "GameObjects", children );

		return json;
	}

	public override void Deserialize( JsonObject node )
	{
		ProcessDeletes();
		Clear();

		using var sceneScope = Push();
		using var spawnScope = SceneUtility.DeferInitializationScope( "Deserialize" );

		if ( node["GameObjects"] is JsonArray childArray )
		{
			foreach ( var child in childArray )
			{
				if ( child is not JsonObject jso )
					return;

				var go = new GameObject();

				go.Parent = this;

				go.Deserialize( jso );
			}
		}
	}

	public virtual GameResource Save()
	{
		var a = new SceneFile();
		a.GameObjects = Children.Select( x => x.Serialize() ).ToArray();
		return a;
	}
}
