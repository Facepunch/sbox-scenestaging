using Sandbox;
using System.Text.Json.Serialization;

public abstract class GameObjectComponent : IPrefabObject.Component
{
	[JsonIgnore]
	public Scene Scene => GameObject.Scene;

	[JsonIgnore]
	public GameObject GameObject { get; internal set; }

	public string Name { get; set; }

	public virtual void DrawGizmos() { }

	public virtual void OnEnabled() { }

	public virtual void OnDisabled() { } 

	protected virtual void OnPostPhysics() { }
	internal void PostPhysics() { OnPostPhysics(); }

	protected virtual void OnPreRender() { }
	internal virtual void PreRender() { OnPreRender(); }
}
