using Sandbox;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

[GameResource( "Prefab 2", PrefabFile.FileExtension, "A prefab", Icon = "ballot" )]
public class PrefabFile : GameResource
{
	public const string FileExtension = "object";

	public JsonObject RootObject { get; set; }

	[JsonIgnore]
	Scene PrefabScene { get; set; }

	[JsonIgnore]
	public GameObject GameObject { get; private set; }

	protected override void PostLoad()
	{
		PostReload();
	}

	protected override void PostReload()
	{
		PrefabScene ??= new Scene();

		using ( PrefabScene.Push() )
		{
			GameObject ??= GameObject.Create();
			GameObject.Deserialize( RootObject );
			GameObject.PrefabSource = this;
		}
	}
}
