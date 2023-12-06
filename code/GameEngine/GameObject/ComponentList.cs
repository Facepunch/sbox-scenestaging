using Sandbox;

/// <summary>
/// Flags to search for Components.
/// I've named this something generic because I think we can re-use it to search for GameObjects too.
/// </summary>
[Flags]
public enum FindMode
{
	/// <summary>
	/// Components that are enabled
	/// </summary>
	Enabled = 1,

	/// <summary>
	/// Components that are disabled
	/// </summary>
	Disabled = 2,

	/// <summary>
	/// Components nn this object
	/// </summary>
	InSelf = 4,

	/// <summary>
	/// Components in our parent
	/// </summary>
	InParent = 8,

	/// <summary>
	/// Components in all ancestors (parent, their parent, their parent, etc)
	/// </summary>
	InAncestors = 16,

	/// <summary>
	/// Components in our children
	/// </summary>
	InChildren = 32,

	/// <summary>
	/// Components in all decendants (our children, their children, their children etc)
	/// </summary>
	InDescendants = 64,


	EnabledInSelf = Enabled | InSelf,
	EnabledInSelfAndDescendants = Enabled | InSelf | InDescendants,
	EnabledInSelfAndChildren = Enabled | InSelf | InChildren,

	DisabledInSelf = Disabled | InSelf,
	DisabledInSelfAndDescendants = Disabled | InSelf | InDescendants,
	DisabledInSelfAndChildren = Disabled | InSelf | InChildren,

	EverythingInSelf = Enabled | InSelf | Disabled,
	EverythingInSelfAndDescendants = Enabled | InSelf | Disabled | InDescendants,
	EverythingInSelfAndChildren = Enabled | InSelf | Disabled | InChildren,
	EverythingInSelfAndParent = Enabled | InSelf | Disabled | InParent,
	EverythingInSelfAndAncestors = Enabled | InSelf | Disabled | InAncestors,
	EverythingInAncestors = Enabled | Disabled | InAncestors,
	EverythingInChildren = Enabled | Disabled | InChildren,
	EverythingInDescendants = Enabled | Disabled | InDescendants,
}

public class ComponentList
{
	readonly GameObject go;

	/// <summary>
	/// This is the hard list of components.
	/// This isn't a HashSet because we need the order to stay.
	/// </summary>
	List<BaseComponent> _list;

	internal ComponentList( GameObject o )
	{
		go = o;
		_list = new List<BaseComponent>();
	}

	/// <summary>
	/// Get all components, including disabled ones
	/// </summary>
	public IEnumerable<BaseComponent> GetAll()
	{
		return _list;
	}

	/// <summary>
	/// Add a component of this type
	/// </summary>
	public BaseComponent Create( TypeDescription type, bool startEnabled = true )
	{
		if ( !type.TargetType.IsAssignableTo( typeof( BaseComponent ) ) )
			return null;

		using var batch = CallbackBatch.StartGroup();

		var t = type.Create<BaseComponent>( null );

		t.GameObject = go;
		_list.Add( t );

		t.Enabled = startEnabled;

		return t;
	}

	/// <summary>
	/// Add a component of this type
	/// </summary>
	public T Create<T>( bool startEnabled = true ) where T : BaseComponent, new()
	{
		using var batch = CallbackBatch.StartGroup();
		var t = new T();

		t.GameObject = go;
		_list.Add( t );

		t.InitializeComponent();
		t.Enabled = startEnabled;
		return t;
	}

	/// <summary>
	/// Get a component of this type
	/// </summary>
	public T Get<T>( FindMode search )
	{
		return GetAll<T>( search ).FirstOrDefault();
	}

	/// <summary>
	/// Get a component of this type
	/// </summary>
	public BaseComponent Get( Type type, FindMode find = FindMode.EnabledInSelf )
	{
		return GetAll( type, find ).FirstOrDefault();
	}

	/// <summary>
	/// Get all components of this type
	/// </summary>
	public IEnumerable<BaseComponent> GetAll( Type type, FindMode find )
	{
		return GetAll<BaseComponent>( find ).Where( x => x.GetType().IsAssignableTo( type ) );
	}

	/// <summary>
	/// Get all components
	/// </summary>
	public IEnumerable<BaseComponent> GetAll( FindMode find ) => GetAll<BaseComponent>( find );

	/// <summary>
	/// Get a list of components on this game object, optionally recurse when deep is true
	/// </summary>
	public IEnumerable<T> GetAll<T>( FindMode find = FindMode.InSelf | FindMode.Enabled | FindMode.InDescendants )
	{
		bool enabledOnly = find.HasFlag( FindMode.Enabled );
		bool disabledOnly = find.HasFlag( FindMode.Disabled );

		if ( enabledOnly == disabledOnly )
		{
			enabledOnly = false;
			disabledOnly = false;
		}

		if ( enabledOnly && !go.Enabled ) yield break;

		//
		// Find in self
		//
		if ( find.HasFlag( FindMode.InSelf ) )
		{
			var q = _list.Where( x => x is not null );
			if ( enabledOnly ) q = q.Where( x => x.Enabled );
			if ( disabledOnly ) q = q.Where( x => !x.Enabled );

			foreach ( var c in q.OfType<T>() )
			{
				yield return c;
			}
		}

		//
		// Find in children
		//
		if ( find.HasFlag( FindMode.InChildren ) || find.HasFlag( FindMode.InDescendants ) )
		{
			var f = find | FindMode.InSelf;
			f &= ~FindMode.InParent;
			f &= ~FindMode.InAncestors;

			//
			// If we're not searching all decendants then take away the Children flag
			// that way the recursion will only search immediate children
			//
			if ( !find.HasFlag( FindMode.InDescendants ) )
			{
				f &= ~FindMode.InChildren;
			}

			foreach ( var child in go.Children )
			{
				if ( child is null ) continue;

				foreach ( var found in child.Components.GetAll<T>( f ) )
				{
					yield return found;
				}
			}
		}

		//
		// Find in parent
		//
		if ( find.HasFlag( FindMode.InParent ) || find.HasFlag( FindMode.InAncestors ) )
		{
			var f = find | FindMode.InSelf;
			f &= ~FindMode.InChildren;
			f &= ~FindMode.InDescendants;
			//
			// If we're not searching all decendants then take away the Children flag
			// that way the recursion will only search immediate children
			//
			if ( !find.HasFlag( FindMode.InAncestors ) )
			{
				f &= ~FindMode.InParent;
			}

			if ( go.Parent is not null && go.Parent is not Scene )
			{
				foreach ( var found in go.Parent.Components.GetAll<T>( f ) )
				{
					yield return found;
				}
			}
		}
	}

	/// <summary>
	/// Try to get this component
	/// </summary>
	public bool TryGet<T>( out T component, FindMode search = FindMode.EnabledInSelf )
	{
		component = Get<T>( search );

		return component is not null;
	}

	/// <summary>
	/// Allows linq style queries
	/// </summary>
	public BaseComponent FirstOrDefault( Func<BaseComponent, bool> value ) => _list.FirstOrDefault( value );

	/// <summary>
	/// Amount of components - including disabled
	/// </summary>
	public int Count => _list.Count;


	public void ForEach<T>( string name, bool includeDisabled, Action<T> action )
	{
		if ( !includeDisabled && !go.Active )
			return;

		for ( int i = _list.Count - 1; i >= 0; i-- )
		{
			BaseComponent c = _list[i];

			if ( !includeDisabled && !c.Enabled )
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

	public void ForEach( string name, bool includeDisabled, Action<BaseComponent> action ) => ForEach<BaseComponent>( name, includeDisabled, action );


	internal void RemoveNull()
	{
		_list.RemoveAll( x => x is null );
	}

	internal void OnDestroyedInternal( BaseComponent baseComponent )
	{
		_list.Remove( baseComponent );
	}

	/// <summary>
	/// Move the position of the component in the list by delta (-1 means up one, 1 means down one)
	/// </summary>
	public void Move( BaseComponent baseComponent, int delta )
	{
		var i = _list.IndexOf( baseComponent );
		if ( i < 0 ) return;

        i += delta;

		if ( i < 0 ) i = 0;
		if ( i >= _list.Count ) i = _list.Count - 1;

		// Move the element
		_list.RemoveAt( _list.IndexOf( baseComponent ) );
		_list.Insert( i, baseComponent );
	}

	//
	// Easy Modes
	//

	/// <summary>
	/// Find component on this gameobject
	/// </summary>
	public T Get<T>( bool includeDisabled = false )
	{
		var f = FindMode.InSelf;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find this component, if it doesn't exist - create it.
	/// </summary>
	public T GetOrCreate<T>( FindMode flags = FindMode.EverythingInSelf ) where T : BaseComponent, new()
	{
		if ( TryGet<T>( out var component, flags ) )
			return component;

		return Create<T>();
	}

	/// <summary>
	/// Find component on this gameobject's ancestors or on self
	/// </summary>
	public T GetInAncestorsOrSelf<T>( bool includeDisabled = false )
	{
		var f = FindMode.InSelf | FindMode.InAncestors;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's ancestors
	/// </summary>
	public T GetInAncestors<T>( bool includeDisabled = false )
	{
		var f =  FindMode.InAncestors;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's decendants or on self
	/// </summary>
	public T GetInDescendantsOrSelf<T>( bool includeDisabled = false )
	{
		var f = FindMode.InSelf | FindMode.InDescendants;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's decendants
	/// </summary>
	public T GetInDescendants<T>( bool includeDisabled = false )
	{
		var f = FindMode.InDescendants;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's immediate children or on self
	/// </summary>
	public T GetInChildrenOrSelf<T>( bool includeDisabled = false )
	{
		var f = FindMode.InSelf | FindMode.InChildren;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's immediate children
	/// </summary>
	public T GetInChildren<T>( bool includeDisabled = false )
	{
		var f = FindMode.InChildren;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's parent or on self
	/// </summary>
	public T GetInParentOrSelf<T>( bool includeDisabled = false )
	{
		var f = FindMode.InSelf | FindMode.InParent;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

	/// <summary>
	/// Find component on this gameobject's parent
	/// </summary>
	public T GetInParent<T>( bool includeDisabled = false )
	{
		var f = FindMode.InParent;
		if ( !includeDisabled ) f |= FindMode.Enabled;

		return Get<T>( f );
	}

}
