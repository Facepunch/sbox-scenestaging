using Sandbox;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

[GameResource( "Prefab 2", PrefabFile.FileExtension, "A prefab", Icon = "ballot" )]
public class PrefabFile : GameResource
{
	public const string FileExtension = "object";

	public JsonObject RootObject { get; set; }

	[JsonIgnore]
	PrefabScene PrefabScene { get; set; }

	[JsonIgnore]
	public GameObject GameObject { get; private set; }

	protected override void PostLoad()
	{
		PostReload();
	}

	protected override void PostReload()
	{
		PrefabScene ??= new PrefabScene();

		using ( PrefabScene.Push() )
		{
			PrefabScene.Source = this;
			PrefabScene.Deserialize( RootObject );
		}
	}
}
