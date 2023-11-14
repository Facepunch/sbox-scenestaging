using Sandbox;
using System.Text.Json.Nodes;

[GameResource( "Scene", "scene", "A scene", Icon = "perm_media" )]
public class SceneFile : GameResource
{
	public JsonObject[] GameObjects { get; set; }
	public JsonObject SceneProperties { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
}
