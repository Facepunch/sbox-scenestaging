using Sandbox;
using System.Text.Json.Nodes;

[GameResource( "Scene", "scene", "A scene", Icon = "perm_media" )]
public class SceneFile : GameResource
{
	public JsonObject[] GameObjects { get; set; }
}
