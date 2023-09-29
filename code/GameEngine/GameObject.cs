using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

public sealed partial class GameObject : IPrefabObject, IPrefabObject.Extendible
{
	public Scene Scene { get; set; }

	public Guid Id { get; private set; }

	[Property]
	public string Name { get; set; } = "Untitled Object";


	bool _enabled = true;

	/// <summary>
	/// Is this gameobject enabled?
	/// </summary>
	[Property]
	public bool Enabled 
	{
		get => _enabled;
		set
		{
			if ( _enabled == value )
				return;

			_enabled = value;
			OnEnableStateChanged();
		}
	}

	Transform _transform = Transform.Zero;

	[Property]
	public Transform Transform 
	{
		get => _transform;
		set
		{
			_transform = value;
		}
	}

	public GameObject()
	{
		Id = Guid.NewGuid();
	}

	public Transform WorldTransform
	{
		get
		{
			if ( Parent is not null )
			{
				return Parent.WorldTransform.ToWorld( Transform );
			}

			return Transform;
		}

		set
		{
			// todo - local to parent etc
			Transform = value;
		}
	}

	public override string ToString()
	{
		return $"GO - {Name}";
	}

	public List<GameObjectComponent> Components = new List<GameObjectComponent>();

	GameObject _parent;

	public GameObject Parent 
	{
		get => _parent;
		set
		{
			if ( _parent == value ) return;

			if ( value is not null )
			{
				if ( value.IsAncestor( this ) )
					return;
			}

			var oldParent = _parent;

			if ( oldParent is not null )
			{
				oldParent.Children.Remove( this );
			}

			_parent = value;

			if ( _parent is not null )
			{
				_parent.Children.Add( this );
				Scene = _parent.Scene;
			}

			if ( isDestroying )
				return;

			if ( Scene is not null )
			{
				Scene.OnParentChanged( this, oldParent, _parent );
			}

			// different parent - active state could have changed
			OnEnableStateChanged();
		}
	}

	public List<GameObject> Children { get; } = new List<GameObject>();

	/// <summary>
	/// Is this gameobject active. For it to be active, it needs to be enabled, all of its ancestors
	/// need to be enabled, and it needs to be in a scene.
	/// </summary>
	public bool Active => Enabled && Scene is not null && (Parent?.Active ?? true);

	internal void OnCreate()
	{
		foreach( var component in Components )
		{
			if ( component is GameObjectComponent goc )
			{
				goc.GameObject = this;
			}

			component.OnEnabled();
		}

		foreach( var child in Children )
		{
			child.OnCreate();
		}
	}

	internal void OnDestroy()
	{
		foreach ( var component in Components )
		{
			component.OnDisabled();

			if ( component is GameObjectComponent goc )
			{
				goc.GameObject = null;
			}
		}

		foreach ( var child in Children )
		{
			child.OnDestroy();
		}
	}

	internal void PostPhysics()
	{
		//Gizmo.Draw.LineSphere( new Sphere( WorldTransform.Position, 3 ) );

		foreach ( var component in Components )
		{
			component.PostPhysics();
		}

		foreach ( var child in Children )
		{
			child.PostPhysics();
		}
	}

	internal void PreRender()
	{

		foreach ( var component in Components )
		{
			component.PreRender();
		}

		foreach( var child in Children )
		{
			child.PreRender();
		}
	}

	public T AddComponent<T>( bool enabled = true ) where T : GameObjectComponent, new()
	{
		var t = new T();

		t.GameObject = this;
		Components.Add( t );

		t.Enabled = enabled;

		return t;
	}

	public T GetComponent<T>( bool enabledOnly = true, bool deep = false ) where T : GameObjectComponent
	{
		return GetComponents<T>( enabledOnly, deep ).FirstOrDefault();
	}
	
	public IEnumerable<T> GetComponents<T>( bool enabledOnly = true, bool deep = false ) where T : GameObjectComponent
	{
		var q = Components.OfType<T>();
		if ( enabledOnly ) q = q.Where( x => x.Enabled );

		foreach ( var c in q )
		{
			yield return c;
		}

		if ( deep )
		{
			foreach ( var child in Children )
			{
				foreach( var found in child.GetComponents<T>( enabledOnly, deep ) )
				{
					yield return found;
				}
			}
		}
	}

	public GameObjectComponent AddComponent( TypeDescription type, bool enabled = true )
	{
		if ( !type.TargetType.IsAssignableTo( typeof( GameObjectComponent ) ) )
			return null;

		var t = type.Create<GameObjectComponent>( null );

		t.GameObject = this;
		Components.Add( t );

		t.Enabled = enabled;

		return t;
	}

	internal void DrawGizmos()
	{
		if ( !Active ) return;

		var parentTx = Gizmo.Transform;

		using ( Gizmo.ObjectScope( this, Transform ) )
		{
			if ( Gizmo.IsSelected )
			{
				DrawTransformGizmos( parentTx );
			}

			bool clicked = Gizmo.WasClicked;

			foreach ( var component in Components )
			{
				using var scope = Gizmo.Scope();

				component.DrawGizmos();
				clicked |= Gizmo.WasClicked;
			}

			if ( clicked )
			{
				Gizmo.Select();
			}

			foreach ( var child in Children )
			{
				child.DrawGizmos();
			}

		}
	}

	void DrawTransformGizmos( Transform parentTransform )
	{
		using var scope = Gizmo.Scope();

		var tx = Transform;

		// use the local position but get rid of local rotation and local scale
		Gizmo.Transform = parentTransform.Add( tx.Position, false );

		Gizmo.Hitbox.DepthBias = 0.1f;

		if ( Gizmo.Settings.EditMode == "position" )
		{
			if ( Gizmo.Control.Position( "position", tx.Position, out var newPos, tx.Rotation ) )
			{
				tx.Position = newPos;
			}
		}

		if ( Gizmo.Settings.EditMode == "rotation" )
		{
			if ( Gizmo.Control.Rotate( "rotation", tx.Rotation, out var newRotation ) )
			{
				tx.Rotation = newRotation;
			}
		}

		if ( Gizmo.Settings.EditMode == "scale" )
		{
			if ( Gizmo.Control.Scale( "scale", tx.Scale, out var newScale ) )
			{
				tx.Scale = newScale.Clamp( 0.001f, 100.0f );
			}
		}

		Transform = tx;
	}

	internal void Tick()
	{

	}

	bool isDestroying;
	bool isDestroyed;

	public void Destroy()
	{
		isDestroying = true;
		Scene?.QueueDelete( this );
	}

	void OnEnableStateChanged()
	{
		foreach( var component in Components )
		{
			component.OnEnableStateChanged();
		}

		foreach ( var child in Children )
		{
			child.OnEnableStateChanged();
		}
	}

	public void DestroyImmediate()
	{
		bool isRoot = Parent == null;
		var scene = Scene;

		DestroyRecursive();

		if ( isRoot && scene is not null )
		{
			scene.Remove( this );
		}
	}

	/// <summary>
	/// We are now disconnected from the scene, we can tell all of our children to disconnect too.
	/// </summary>
	void DestroyRecursive()
	{
		isDestroying = true;
		Parent = null;
		Enabled = false;
		Scene = null;
		isDestroyed = true;

		foreach ( var child in Children )
		{
			child.DestroyRecursive();
		}
	}


	public bool IsDescendant( GameObject o )
	{
		return o.IsAncestor( this );
	}

	public bool IsAncestor( GameObject o )
	{
		if ( o == this ) return true;

		if ( Parent is not null )
		{
			return Parent.IsAncestor( o );
		}

		return false;
	}

	public void AddSibling( GameObject go, bool before )
	{
		go.Parent = Parent;

		if ( go.Parent is null )
		{
			Scene.All.Remove( go );

			var targetIndex = Scene.All.IndexOf( this );
			if ( !before ) targetIndex++;
			Scene.All.Insert( targetIndex, go );
		}
		else
		{
			go.Parent.Children.Remove( go );
			var targetIndex = go.Parent.Children.IndexOf( this );
			if ( !before ) targetIndex++;
			go.Parent.Children.Insert( targetIndex, go );
		}
	}
}
