﻿using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using static Sandbox.IPrefabObject;

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

public partial class GameObject
{
	protected Scene _scene;
	private GameTransform _transform;

	public Scene Scene => _scene;
	public GameTransform Transform => _transform;

	public Guid Id { get; private set; }

	[Property]
	public string Name { get; set; } = "Untitled Object";

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

			SceneUtility.ActivateGameObject( this );
		}
	}

	internal GameObject( bool enabled, string name, Scene scene )
	{
		_transform = new GameTransform( this );
		_enabled = enabled;
		_scene = scene;
		Id = Guid.NewGuid();
		Name = name;
	}

	public static GameObject Create( bool enabled = true, string name = "GameObject" )
	{
		if ( GameManager.ActiveScene is null )
			throw new System.ArgumentNullException( "Trying to create a GameObject without an active scene" );

		return new GameObject( enabled, name, GameManager.ActiveScene );
	}
		
	public override string ToString()
	{
		return $"GameObject:{Name}";
	}

	public List<BaseComponent> Components = new List<BaseComponent>();

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
				Assert.True( Scene == _parent.Scene, "Can't parent to a gameobject in a different scene" );
				_parent.Children.Add( this );
			}
		}
	}

	public List<GameObject> Children { get; } = new List<GameObject>();

	/// <summary>
	/// Is this gameobject active. For it to be active, it needs to be enabled, all of its ancestors
	/// need to be enabled, and it needs to be in a scene.
	/// </summary>
	public bool Active => Enabled && Scene is not null && (Parent?.Active ?? true);

	internal void OnDestroy()
	{
		ForEachComponent( "OnDestroy", true, c => c.OnDestroyInternal() );

		foreach ( var child in Children )
		{
			child.OnDestroy();
		}
	}

	internal void PostPhysics()
	{
		//Gizmo.Draw.LineSphere( new Sphere( WorldTransform.Position, 3 ) );

		ForEachComponent( "PostPhysics", true, c => c.PostPhysics() );

		foreach ( var child in Children )
		{
			child.PostPhysics();
		}
	}

	internal void PreRender()
	{
		ForEachComponent( "PreRender", true, c => c.PreRender() );

		foreach ( var child in Children )
		{
			child.PreRender();
		}
	}

	internal void ForEachComponent( string name, bool activeOnly, Action<BaseComponent> action )
	{
		for ( int i = 0; i < Components.Count; i++ )
		{
			var c = Components[i];

			if ( c is null )
			{
				Components.RemoveAt( i );
				i--;
				continue;
			}

			if ( activeOnly && !c.Active )
				continue;

			try
			{
				action( c );
			}
			catch ( System.Exception e )
			{
				Log.Warning( e, $"Exception when calling {name} on {c}: {e.Message}" );
			}
		}
	}

	public T AddComponent<T>( bool enabled = true ) where T : BaseComponent, new()
	{
		var t = new T();

		t.GameObject = this;
		Components.Add( t );

		t.Enabled = enabled;
		return t;
	}

	/// <summary>
	/// Get the first matching component on this game object, optionally recurse when deep is true
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="enabledOnly"></param>
	/// <param name="deep"></param>
	/// <returns></returns>
	public T GetComponent<T>( bool enabledOnly = true, bool deep = false )
	{
		return GetComponents<T>( enabledOnly, deep ).FirstOrDefault();
	}

	/// <summary>
	/// Get a list of components on this game object, optionally recurse when deep is true
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="enabledOnly"></param>
	/// <param name="deep"></param>
	/// <returns></returns>
	public IEnumerable<T> GetComponents<T>( bool enabledOnly = true, bool deep = false )
	{
		var q = Components.AsEnumerable();
		if ( enabledOnly ) q = q.Where( x => x.Active );

		foreach ( var c in q.OfType<T>() )
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

	public bool TryGetComponent<T>( out T component )
	{
		component = GetComponent<T>();

		return component is not null;
	}

	public BaseComponent AddComponent( TypeDescription type, bool enabled = true )
	{
		if ( !type.TargetType.IsAssignableTo( typeof( BaseComponent ) ) )
			return null;

		var t = type.Create<BaseComponent>( null );

		t.GameObject = this;
		Components.Add( t );

		t.Enabled = enabled;

		return t;
	}

	internal virtual void Tick()
	{
		if ( !Enabled )
			return;

		OnUpdate();

		for ( int i=0; i < Children.Count; i++ )
		{
			Children[i].Tick();
		}
	}

	bool isDestroying;
	bool isDestroyed;

	public void Destroy()
	{
		isDestroying = true;
		Scene?.QueueDelete( this );
	}

	/// <summary>
	/// Should be called whenever we change anything that we suspect might
	/// cause the active status to change on us, or our components. Don't call
	/// this directly. Only call it via SceneUtility.ActivateGameObject( this );
	/// </summary>
	internal void UpdateEnabledStatus()
	{
		ForEachComponent( "UpdateEnabledStatus", false, c =>
		{
			c.GameObject = this;
			c.UpdateEnabledStatus();
		} );

		foreach ( var child in Children )
		{
			child.UpdateEnabledStatus();
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
		_scene = null;
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
		if ( this is Scene ) throw new InvalidOperationException( "Can't add a sibling to a scene!" );

		go.SetParent( Parent, keepWorldPosition );

		go.Parent.Children.Remove( go );
		var targetIndex = go.Parent.Children.IndexOf( this );
		if ( !before ) targetIndex++;
		go.Parent.Children.Insert( targetIndex, go );
	}

	public void SetParent( GameObject value, bool keepWorldPosition = true )
	{
		if ( this is Scene ) throw new InvalidOperationException( "Can't set the parent of a scene!" );

		if ( Parent == value ) return;

		if ( keepWorldPosition )
		{
			var wp = Transform.World;
			Parent = value;
			Transform.World = wp;
		}
		else
		{
			Parent = value;
		}
	}

	/// <summary>
	/// Find component on this gameobject, or its parents
	/// </summary>
	public T GetComponentInParent<T>( bool enabledOnly = true ) where T : BaseComponent
	{
		var t = GetComponent<T>( enabledOnly, false );
		if ( t is not null )
			return t;

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

		return Enumerable.Empty<GameObject>();
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
		ForEachComponent( "Update", true, c => c.InternalUpdate() );
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

	public IEnumerable<GameObject> GetAllObjects( bool enabled )
	{
		if ( enabled && !Enabled )
			yield break;

		yield return this;

		foreach ( var child in Children.SelectMany( x => x.GetAllObjects( enabled ) ) )
		{
			yield return child;
		}
	}

	public virtual void EditLog( string name, object source, Action undo )
	{
		if ( Parent == null ) return;

		Parent.EditLog( name, source, undo );
	}

	/// <summary>
	/// This is slow, and somewhat innacurate. Don't call it every frame!
	/// </summary>
	public BBox GetBounds()
	{
		var renderers = GetComponents<ModelComponent>( true, true );

		return BBox.FromBoxes( renderers.Select( x => x.Bounds ) );
	}
}
