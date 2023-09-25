using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public sealed class GameObject : IPrefabObject, IPrefabObject.Extendible
{
	public Scene Scene { get; set; }

	[Property]
	public string Name { get; set; } = "Untitled Object";

	[Property]
	public bool Enabled { get; set; } = true;

	[Property]
	public Transform Transform { get; set; } = Transform.Zero;

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

	public List<GameObjectComponent> Components = new List<GameObjectComponent>();

	public T GetComponent<T>( bool allowInactive = false ) where T : GameObjectComponent
	{
		return Components.OfType<T>().FirstOrDefault();
	}

	GameObject _parent;

	public GameObject Parent 
	{
		get => _parent;
		set
		{
			if ( _parent == value ) return;

			var oldParent = _parent;

			if ( _parent is not null )
			{
				_parent.Children.Remove( this );
			}

			_parent = value;

			if ( _parent is not null )
			{
				_parent.Children.Add( this );
			}

			if ( Scene is not null )
			{
				Scene.OnParentChanged( this, oldParent, _parent );
			}
		}
	}

	public HashSet<GameObject> Children { get; } = new HashSet<GameObject>();


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
		foreach ( var component in Components )
		{
			component.DrawGizmos();
		}

		foreach ( var child in Children )
		{
			child.DrawGizmos();
		}
	}

	internal void Tick()
	{

	}
}
