using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GameObject
{

	/// <summary>
	/// Get the first matching component on this game object, optionally recurse when deep is true
	/// </summary>
	public BaseComponent GetComponent( Type type, bool enabledOnly = true, bool deep = false )
	{
		return GetComponents( type, enabledOnly, deep ).FirstOrDefault();
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
		var q = Components.Where( x => x is not null );
		if ( enabledOnly ) q = q.Where( x => x.Active );

		foreach ( var c in q.OfType<T>() )
		{
			yield return c;
		}

		if ( deep )
		{
			foreach ( var child in Children )
			{
				if ( child is null ) continue;

				foreach ( var found in child.GetComponents<T>( enabledOnly, deep ) )
				{
					yield return found;
				}
			}
		}
	}

	/// <summary>
	/// Get a list of components on this game object, optionally recurse when deep is true
	/// </summary>
	public IEnumerable<BaseComponent> GetComponents( Type type, bool enabledOnly = true, bool deep = false )
	{
		return GetComponents<BaseComponent>( enabledOnly, deep ).Where( x => x.GetType().IsAssignableTo( type ) );
	}

	public bool TryGetComponent<T>( out T component, bool enabledOnly = true, bool deep = false )
	{
		component = GetComponent<T>( enabledOnly, deep );

		return component is not null;
	}

	/// <summary>
	/// Find component on this gameobject, or its parents
	/// </summary>
	public T GetComponentInParent<T>( bool enabledOnly = true, bool andSelf = false )
	{
		if ( andSelf )
		{
			var t = GetComponent<T>( enabledOnly, false );
			if ( t is not null )
				return t;
		}

		if ( Parent is not null )
		{
			return Parent.GetComponentInParent<T>( enabledOnly, true );
		}

		return default;
	}

	public void ForEachComponent( string name, bool activeOnly, Action<BaseComponent> action )
	{
		for ( int i = Components.Count - 1; i >= 0; i-- )
		{
			var c = Components[i];

			if ( c is null )
			{
				Components.RemoveAt( i );
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

	public void ForEachComponent<T>( string name, bool activeOnly, Action<T> action )
	{
		for ( int i = Components.Count - 1; i >= 0; i-- )
		{
			BaseComponent c = Components[i];

			if ( activeOnly && !c.Active )
				continue;

			if ( c is null )
			{
				Components.RemoveAt( i );
				continue;
			}

			if ( c is not T t )
				continue;

			try
			{
				action( t );
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

		t.InitializeComponent();
		t.Enabled = enabled;
		return t;
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
}
