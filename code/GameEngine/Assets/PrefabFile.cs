using Sandbox;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

[GameResource( "Prefab 2", PrefabFile.FileExtension, "A prefab", Icon = "ballot" )]
public class PrefabFile : GameResource
{
	public const string FileExtension = "object";

	public JsonObject RootObject { get; set; }

	/// <summary>
	/// This is used as a reference
	/// </summary>
	[JsonIgnore]
	public PrefabScene Scene { get; set; }

	protected override void PostLoad()
	{
		PostReload();
	}

	protected override void PostReload()
	{
		Scene ??= PrefabScene.Create( this );
	}
}
