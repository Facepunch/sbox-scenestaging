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

	public string Id { get; private set; }

	[Property]
	public string Name { get; set; } = "Untitled Object";

	[Property]
	public bool Enabled { get; set; } = true;

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
		Id = Guid.NewGuid().ToString();
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

			var oldParent = _parent;

			if ( oldParent is not null )
			{
				oldParent.Children.Remove( this );
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

	public T GetComponent<T>( bool enabledOnly = true ) where T : GameObjectComponent
	{
		var q = Components.OfType<T>();

		if ( enabledOnly ) q = q.Where( x => x.Enabled );

		return q.FirstOrDefault();
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
		using ( Gizmo.ObjectScope( this, Transform ) )
		{
			if ( Gizmo.IsSelected )
			{
				if ( Gizmo.Control.Position( "position", Vector3.Zero, out var position ) )
				{
					Transform = Transform.WithPosition( Transform.Position + position * Transform.Rotation );
				}
			}

			foreach ( var component in Components )
			{
				component.DrawGizmos();
			}

			if ( Gizmo.WasClicked )
			{
				Gizmo.Select();
			}

			foreach ( var child in Children )
			{
				child.DrawGizmos();
			}

		}
	}

	internal void Tick()
	{

	}

	public void Destroy()
	{
		Scene?.QueueDelete( this );
	}
}
