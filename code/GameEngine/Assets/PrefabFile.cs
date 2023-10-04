using Sandbox;
using System.Text.Json.Nodes;

[GameResource( "Prefab 2", PrefabFile.FileExtension, "A prefab", Icon = "ballot" )]
public class PrefabFile : GameResource
{
	public const string FileExtension = "object";

	public JsonObject RootObject { get; set; }

	public GameObject GameObject { get; private set; }

	protected override void PostLoad()
	{
		PostReload();
	}

	protected override void PostReload()
	{
		Log.Info( "PostReload" );
		// clear etc
		GameObject ??= new GameObject();
		GameObject.Deserialize( RootObject );
		GameObject.PrefabSource = this;

		Log.Info( $"GameObject = {GameObject}" );
	}
}
