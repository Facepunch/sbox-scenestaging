using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sandbox;

/// <summary>
/// Clutter component for organizing and managing scattered objects
/// </summary>
public sealed class ClutterComponent : Component, Component.ExecuteInEditor
{
	[Property, Hide, JsonIgnore] public List<ClutterLayer> Layers { get; set; } = [];

	public override string ToString() => GameObject.Name;
}
