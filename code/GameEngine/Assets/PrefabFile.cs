using Sandbox;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

[GameResource( "Prefab 2", PrefabFile.FileExtension, "A prefab", Icon = "ballot" )]
public class PrefabFile : GameResource
{
	public const string FileExtension = "object";

	public JsonObject RootObject { get; set; }

	[JsonIgnore]
	public PrefabScene PrefabScene { get; set; }

	protected override void PostLoad()
	{
		PostReload();
	}

	public void UpdateJson()
	{
		RootObject = PrefabScene.Serialize();
	}

	protected override void PostReload()
	{
		PrefabScene ??= new PrefabScene();

		using ( PrefabScene.Push() )
		{
			PrefabScene.Load( this );
		}
	}
}
