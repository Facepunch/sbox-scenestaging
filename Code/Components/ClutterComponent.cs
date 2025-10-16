using System.Text.Json.Nodes;

namespace Sandbox;

/// <summary>
/// Clutter component for organizing and managing scattered objects
/// </summary>
public sealed class ClutterComponent : Component, Component.ExecuteInEditor
{
	[Property] public List<ClutterLayer> Layers { get; set; } = [];

	public override string ToString() => GameObject.Name;

	/// <summary>
	/// Destroys a clutter instance if it's a valid prefab GameObject
	/// </summary>
	public static void DestroyInstance( ClutterInstance instance )
	{
		if ( instance.ClutterType == ClutterInstance.Type.Prefab &&
			 instance.gameObject != null &&
			 instance.gameObject.IsValid() )
		{
			instance.gameObject.Destroy();
		}
	}
}
