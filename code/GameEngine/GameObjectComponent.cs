using Sandbox;
using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public abstract partial class GameObjectComponent : IPrefabObject.Component
{
	[JsonIgnore]
	public Scene Scene => GameObject.Scene;

	[JsonIgnore]
	public GameObject GameObject { get; internal set; }

	bool _enabledState;
	bool _enabled = false;

	public bool Enabled
	{
		get => _enabled;

		set
		{
			if ( _enabled == value ) return;

			_enabled = value;
			OnEnableStateChanged();
		}
	}

	public string Name { get; set; }

	public virtual void DrawGizmos() { }

	public virtual void OnEnabled() { }

	public virtual void OnDisabled() { } 

	protected virtual void OnPostPhysics() { }
	internal void PostPhysics() { OnPostPhysics(); }

	protected virtual void OnPreRender() { }
	internal virtual void PreRender() { OnPreRender(); }

	internal void OnEnableStateChanged()
	{
		var state = _enabled && Scene is not null && GameObject is not null && GameObject.Active;
		if ( state  == _enabledState ) return;

		_enabledState = state;

		if ( _enabledState )
		{
			OnEnabled();
		}
		else
		{
			OnDisabled();
		}
	}
}
