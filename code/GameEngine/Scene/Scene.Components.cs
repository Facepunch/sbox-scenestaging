using Sandbox;

public partial class Scene : GameObject
{
	Dictionary<Type, HashSet<Component>> components = new();

	internal void RegisterComponent( Component c )
	{
		var t = c.GetType();
		while ( t != typeof( Component ) )
		{
			components.GetOrCreate( t ).Add( c );

			t = t.BaseType;
		}
	}

	internal void UnregisterComponent( Component c )
	{
		var t = c.GetType();
		while ( t != typeof( Component ) )
		{
			components.GetOrCreate( t ).Remove( c );

			t = t.BaseType;
		}
	}

	public IEnumerable<T> GetAllComponents<T>() where T : Component
	{
		if ( !components.TryGetValue( typeof( T ), out var set ) )
			return Array.Empty<T>();

		return set.OfType<T>();
	}
}
