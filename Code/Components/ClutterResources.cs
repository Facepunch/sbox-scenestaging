namespace Sandbox;

/// <summary>
/// Handles loading, caching, and instantiation of clutter resources (prefabs and models)
/// </summary>
public class ClutterResources
{
	private readonly Dictionary<string, object> _cache = new();

	/// <summary>
	/// Creates a clutter instance from a ClutterObject
	/// </summary>
	public ClutterInstance? CreateInstance( ClutterObject clutterObject, Transform transform )
	{
		return CreateInstance( clutterObject.Path, transform, clutterObject.IsSmall );
	}

	/// <summary>
	/// Creates a clutter instance from a path
	/// </summary>
	public ClutterInstance? CreateInstance( string path, Transform transform, bool isSmall = false )
	{
		var resource = LoadResource( path );
		if ( resource == null )
			return null;

		return CreateInstanceFromResource( resource, transform, isSmall );
	}

	/// <summary>
	/// Loads a resource from the given path, using cache if available
	/// </summary>
	public object LoadResource( string path )
	{
		// Try to get from cache first
		if ( _cache.TryGetValue( path, out var cachedResource ) )
		{
			return cachedResource;
		}

		// Load the resource
		object resource = TryLoadPrefab( path ) ?? TryLoadModel( path );

		if ( resource == null )
		{
			Log.Warning( $"Failed to load resource at path: {path}" );
			return null;
		}

		// Cache it
		CacheResource( path, resource );

		return resource;
	}

	/// <summary>
	/// Preloads multiple resource paths into the cache
	/// </summary>
	public void Preload( IEnumerable<string> paths )
	{
		foreach ( var path in paths.Where( p => !_cache.ContainsKey( p ) ) )
		{
			LoadResource( path );
		}
	}

	/// <summary>
	/// Clears the resource cache
	/// </summary>
	public void Clear()
	{
		_cache.Clear();
	}

	/// <summary>
	/// Gets the number of cached resources
	/// </summary>
	public int Count => _cache.Count;

	private object TryLoadPrefab( string path )
	{
		// Try prefab if path ends with .prefab or has no extension
		if ( path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
			return null;

		return ResourceLibrary.Get<PrefabFile>( path );
	}

	private object TryLoadModel( string path )
	{
		// Try model if path ends with .vmdl or has no extension (after prefab failed)
		if ( path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
			return null;

		return Model.Load( path );
	}

	private void CacheResource( string path, object resource )
	{
		_cache[path] = resource;

		// Also cache models by their ResourcePath if different
		if ( resource is Model model && model.ResourcePath != null && model.ResourcePath != path )
		{
			_cache[model.ResourcePath] = model;
		}
	}

	private ClutterInstance? CreateInstanceFromResource( object resource, Transform transform, bool isSmall )
	{
		return resource switch
		{
			PrefabFile prefab => new ClutterInstance( SceneUtility.GetPrefabScene( prefab ).Clone( transform ), transform, isSmall ),
			Model model => new ClutterInstance( model, transform, isSmall ),
			_ => null
		};
	}
}
