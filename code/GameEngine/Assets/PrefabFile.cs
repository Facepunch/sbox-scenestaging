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

	/// <summary>
	/// If true then we'll show this in the right click menu, so people can create it
	/// </summary>
	public bool ShowInMenu { get; set; }

	/// <summary>
	/// If ShowInMenu is true, this is the path in the menu for this prefab
	/// </summary>
	public string MenuPath { get; set; }

	/// <summary>
	/// Icon to show to the left of the option in the menu
	/// </summary>
	public string MenuIcon { get; set; }
}
