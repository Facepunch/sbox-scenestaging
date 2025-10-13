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
	public ClutterInstance? CreateInstance( ClutterObject clutterObject, Transform transform, GameObject? parent = null )
	{
		return CreateInstance( clutterObject.Path, transform, clutterObject.IsSmall, parent );
	}

	/// <summary>
	/// Creates a clutter instance from a path. Returns null if the resource fails to load.
	/// </summary>
	public ClutterInstance? CreateInstance( string path, Transform transform, bool isSmall = false, GameObject? parent = null )
	{
		var resource = TryLoadResource( path );
		return resource != null ? CreateInstanceFromResource( resource, transform, isSmall, parent ) : null;
	}

	/// <summary>
	/// Loads a resource from the given path, using cache if available
	/// </summary>
	public object? TryLoadResource( string path )
	{
		// Try to get from cache first
		if ( _cache.TryGetValue( path, out var cachedResource ) )
			return cachedResource;

		// Load the resource
		var resource = TryLoadPrefab( path ) ?? TryLoadModel( path );

		if ( resource == null )
		{
			Log.Warning( $"Failed to load resource at path: {path}" );
			return null;
		}

		// Cache and return
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
			TryLoadResource( path );
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

	private ClutterInstance? CreateInstanceFromResource( object resource, Transform transform, bool isSmall, GameObject? parent )
	{
		return resource switch
		{
			PrefabFile prefab => CreatePrefabInstanceData( prefab, transform, isSmall, parent ),
			Model model => new ClutterInstance( model, transform, isSmall ),
			_ => null
		};
	}

	/// <summary>
	/// Creates a clutter instance data for a prefab. The actual GameObject instantiation happens later on the main thread.
	/// </summary>
	private ClutterInstance CreatePrefabInstanceData( PrefabFile prefab, Transform transform, bool isSmall, GameObject? parent )
	{
		// Store prefab reference and parent for later instantiation on main thread
		return new ClutterInstance( prefab, transform, isSmall, parent );
	}
}
