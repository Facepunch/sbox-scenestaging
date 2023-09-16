using Sandbox;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public sealed class GameObject : IPrefabObject, IPrefabObject.Extendible
{
	internal Scene Scene { get; set; }

	[Property]
	public string Name { get; set; }

	[Property]
	public Transform Transform { get; set; }

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

	public HashSet<GameObjectComponent> Components = new HashSet<GameObjectComponent>();

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

			if ( _parent is not null )
			{
				_parent.Children.Remove( this );
			}

			_parent = value;

			if ( _parent is not null )
			{
				_parent.Children.Add( this );
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
}
