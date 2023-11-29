using Sandbox;

public class ComponentList
{
	readonly GameObject go;
	List<BaseComponent> _list = new List<BaseComponent>();

	internal ComponentList( GameObject o )
	{
		go = o;
	}

	/// <summary>
	/// Get all components
	/// </summary>
	public IEnumerable<BaseComponent> GetAll()
	{
		return _list;
	}

	/// <summary>
	/// Add a component of this type
	/// </summary>
	public BaseComponent Create( TypeDescription type, bool enabled = true )
	{
		if ( !type.TargetType.IsAssignableTo( typeof( BaseComponent ) ) )
			return null;

		using var batch = CallbackBatch.StartGroup();

		var t = type.Create<BaseComponent>( null );

		t.GameObject = go;
		_list.Add( t );

		t.Enabled = enabled;

		return t;
	}

	/// <summary>
	/// Add a component of this type
	/// </summary>
	public T Create<T>( bool enabled = true ) where T : BaseComponent, new()
	{
		using var batch = CallbackBatch.StartGroup();
		var t = new T();

		t.GameObject = go;
		_list.Add( t );

		t.InitializeComponent();
		t.Enabled = enabled;
		return t;
	}

	/// <summary>
	/// Get a component, of this type
	/// </summary>
	public T Get<T>( bool enabledOnly = true, bool deep = false )
	{
		return GetAll<T>( enabledOnly, deep ).FirstOrDefault();
	}


	public BaseComponent Get( Type type, bool enabledOnly = true, bool deep = false )
	{
		return GetAll( type, enabledOnly, deep ).FirstOrDefault();
	}

	public IEnumerable<BaseComponent> GetAll( Type type, bool enabledOnly = true, bool deep = false )
	{
		return GetAll<BaseComponent>( enabledOnly, deep ).Where( x => x.GetType().IsAssignableTo( type ) );
	}

	/// <summary>
	/// Get a list of components on this game object, optionally recurse when deep is true
	/// </summary>
	public IEnumerable<T> GetAll<T>( bool enabledOnly = true, bool deep = false )
	{
		if ( enabledOnly && !go.Enabled ) yield break;

		var q = _list.Where( x => x is not null );
		if ( enabledOnly ) q = q.Where( x => x.Active );

		foreach ( var c in q.OfType<T>() )
		{
			yield return c;
		}

		if ( deep )
		{
			foreach ( var child in go.Children )
			{
				if ( child is null ) continue;

				foreach ( var found in child.Components.GetAll<T>( enabledOnly, deep ) )
				{
					yield return found;
				}
			}
		}
	}

	/// <summary>
	/// Try to get this component
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="component"></param>
	/// <param name="enabledOnly"></param>
	/// <param name="deep"></param>
	/// <returns></returns>
	public bool TryGet<T>( out T component, bool enabledOnly = true, bool deep = false )
	{
		component = Get<T>( enabledOnly, deep );

		return component is not null;
	}

	public BaseComponent FirstOrDefault( Func<BaseComponent, bool> value ) => _list.FirstOrDefault( value );
	public int Count => _list.Count;

	internal void OnDestroyedInternal( BaseComponent baseComponent )
	{
		_list.Remove( baseComponent );
	}

	/// <summary>
	/// Find component on this gameobject, or its parents
	/// </summary>
	public T GetInParent<T>( bool enabledOnly = true, bool andSelf = false )
	{
		if ( andSelf )
		{
			var t = Get<T>( enabledOnly, false );
			if ( t is not null )
				return t;
		}

		if ( go.Parent is not null )
		{
			return go.Parent.Components.GetInParent<T>( enabledOnly, true );
		}

		return default;
	}

	public void ForEach<T>( string name, bool activeOnly, Action<T> action )
	{
		for ( int i = _list.Count - 1; i >= 0; i-- )
		{
			BaseComponent c = _list[i];

			if ( activeOnly && !c.Active )
				continue;

			if ( c is null )
			{
				_list.RemoveAt( i );
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

	public void ForEach( string name, bool activeOnly, Action<BaseComponent> action )
		=> ForEach<BaseComponent>( name, activeOnly, action );

	internal void RemoveNull()
	{
		_list.RemoveAll( x => x is null );
	}
}
