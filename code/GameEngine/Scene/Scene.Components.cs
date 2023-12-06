using Sandbox;

public partial class Scene : GameObject
{
	Dictionary<Type, HashSet<BaseComponent>> components = new();

	internal void RegisterComponent( BaseComponent c )
	{
		var t = c.GetType();
		while ( t != typeof( BaseComponent ) )
		{
			components.GetOrCreate( t ).Add( c );

			t = t.BaseType;
		}
	}

	internal void UnregisterComponent( BaseComponent c )
	{
		var t = c.GetType();
		while ( t != typeof( BaseComponent ) )
		{
			components.GetOrCreate( t ).Remove( c );

			t = t.BaseType;
		}
	}

	public IEnumerable<T> GetAllComponents<T>() where T : BaseComponent
	{
		if ( !components.TryGetValue( typeof( T ), out var set ) )
			return Array.Empty<T>();

		return set.OfType<T>();
	}
}
