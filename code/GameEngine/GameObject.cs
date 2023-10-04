using Sandbox;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public enum GameObjectFlags
{
	None = 0,

	/// <summary>
	/// Hide this object in heirachy/inspector
	/// </summary>
	Hidden = 1,

	/// <summary>
	/// Don't save this object to disk, or when duplicating
	/// </summary>
	NotSaved = 2,
}

public sealed partial class GameObject : IPrefabObject, IPrefabObject.Extendible
{

	Scene _scene;

	public Scene Scene 
	{
		get => _scene;
		set
		{
			if ( _scene == value )
				return;

			_scene = value;

			foreach ( var child in Children )
			{
				child.Scene = _scene;
			}
		}
	}

	public Guid Id { get; private set; }

	[Property]
	public string Name { get; set; } = "Untitled Object";

	public PrefabFile PrefabSource { get; set; }


	public GameObjectFlags Flags { get; set; } = GameObjectFlags.None;

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
			if ( Parent is not null )
			{
				Transform = Parent.WorldTransform.ToLocal( value );
				return;
			}

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
				MoveToScene( _parent.Scene );
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
		foreach ( var component in Components )
		{
			if ( component is GameObjectComponent goc )
			{
				goc.GameObject = this;
			}

			component.OnEnabled();
		}

		foreach ( var child in Children )
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

		foreach ( var child in Children )
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
				foreach ( var found in child.GetComponents<T>( enabledOnly, deep ) )
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
		if ( !Enabled )
			return;

		OnUpdate();

		foreach ( var child in Children )
		{
			child.Tick();
		}
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
		foreach ( var component in Components )
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

		foreach ( var child in Children.ToArray() )
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

	public void AddSibling( GameObject go, bool before, bool keepWorldPosition = true )
	{
		go.MoveToScene( Scene );
		go.SetParent( Parent, keepWorldPosition );

		if ( go.Parent is null )
		{
			Scene.All.Remove( go );

			var targetIndex = Scene.All.IndexOf( this );
			if ( !before ) targetIndex++;
			Scene.All.Insert( targetIndex, go );

			go.OnEnableStateChanged();
		}
		else
		{
			go.Parent.Children.Remove( go );
			var targetIndex = go.Parent.Children.IndexOf( this );
			if ( !before ) targetIndex++;
			go.Parent.Children.Insert( targetIndex, go );
		}
	}

	private void MoveToScene( Scene scene )
	{
		if ( Scene == scene )
			return;

		// todo tell old scene we're no longer on it?
		Scene = scene;

		foreach ( var child in Children )
		{
			child.MoveToScene( scene );
		}
	}

	public void SetParent( GameObject value, bool keepWorldPosition = true )
	{
		if ( Parent == value ) return;

		if ( keepWorldPosition )
		{
			var wp = WorldTransform;
			Parent = value;
			WorldTransform = wp;
		}
		else
		{
			Parent = value;
		}
	}

	/// <summary>
	/// Find component on this gameobject, or its parents
	/// </summary>
	public T GetComponentInParent<T>( bool enabledOnly = true ) where T : GameObjectComponent
	{
		var t = GetComponent<T>( enabledOnly, false );
		if ( t is not null ) return t;

		if ( Parent is not null )
		{
			return Parent.GetComponentInParent<T>( enabledOnly );
		}

		return null;
	}

	IEnumerable<GameObject> GetSiblings()
	{
		if ( Parent is not null )
		{
			return Parent.Children.Where( x => x != this );
		}

		return Scene.All.Where( x => x != this );
	}

	// todo - this should be internal
	public void MakeNameUnique()
	{
		var names = GetSiblings().Select( x => x.Name ).ToHashSet();

		if ( !names.Contains( Name ) )
			return;

		var targetName = Name;

		// todo regex (number)

		if ( targetName.Contains( '(' ) )
		{
			targetName = targetName.Substring( 0, targetName.LastIndexOf( '(' ) ).Trim();
		}

		for ( int i = 1; i < 500; i++ )
		{
			var indexedName = $"{targetName} ({i})";

			if ( !names.Contains( indexedName ) )
			{
				Name = indexedName;
				return;
			}
		}
	}

	void OnUpdate()
	{
		foreach ( var c in Components )
		{
			c.InternalUpdate();
		}
	}

	/// <summary>
	/// Find a GameObject by Guid
	/// </summary>
	public GameObject FindObjectByGuid( Guid guid )
	{
		if ( guid == Id )
			return this;

		return Children.Select( x => x.FindObjectByGuid( guid ) )
								.Where( x => x is not null )
								.FirstOrDefault();
	}
}
