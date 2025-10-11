using System.Text.Json.Nodes;
using static Sandbox.ClutterInstance;

namespace Sandbox;

/// <summary>
/// Clutter component for organizing and managing scattered objects
/// </summary>
public sealed class ClutterComponent : Component, Component.ExecuteInEditor
{
	[Property] public List<ClutterLayer> Layers { get; set; } = new();

	[Property, Hide]
	public string SerializedData { get; set; }

	public override string ToString() => GameObject.Name;

	protected override void OnEnabled()
	{
		// Deserialize when component is enabled (before scene systems initialize)
		if ( !string.IsNullOrEmpty( SerializedData ) )
		{
			try
			{
				JsonObject json = JsonNode.Parse( SerializedData ) as JsonObject;
				if ( json != null )
				{
					ClutterSerializer.Deserialize( this, json );
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"Failed to deserialize ClutterComponent: {ex.Message}" );
			}
		}
	}

	protected override void OnDestroy()
	{
		SerializeToProperty();
	}

	/// <summary>
	/// Serializes the current state to the SerializedData property
	/// </summary>
	public void SerializeToProperty()
	{
		var json = ClutterSerializer.Serialize( this );
		SerializedData = json.ToJsonString();
	}

	/// <summary>
	/// Destroys a clutter instance if it's a valid prefab GameObject
	/// </summary>
	public static void DestroyInstance( ClutterInstance instance )
	{
		if ( instance.ClutterType == ClutterInstance.Type.Prefab &&
			 instance.gameObject != null &&
			 instance.gameObject.IsValid() )
		{
			instance.gameObject.Destroy();
		}
	}

	/// <summary>
	/// Removes an instance from the given layers by InstanceId
	/// </summary>
	public static void RemoveInstanceFromLayers( ClutterInstance instance, List<ClutterInstance.ClutterLayer> layers )
	{
		foreach ( var layer in layers )
		{
			layer.Instances?.RemoveAll( i => i.InstanceId == instance.InstanceId );
		}
	}
}

public struct ClutterInstance
{
	public Transform transform;

	public GameObject gameObject = null;
	public Model model = null;

	public enum Type
	{
		Prefab, Model
	}
	public Type ClutterType { get; private set; }
	public Guid InstanceId { get; private set; }
	public float Size { get; private set; }
	public bool IsSmall { get; set; }

	public ClutterInstance( GameObject go, Transform t, bool isSmall = false )
	{
		InstanceId = Guid.NewGuid();
		transform = t;
		gameObject = go;
		ClutterType = Type.Prefab;
		IsSmall = isSmall;

		// Calculate size from bounds
		var bbox = go.GetBounds();
		Size = bbox.Size.Length;
	}

	public ClutterInstance( Model m, Transform t, bool isSmall = false )
	{
		InstanceId = Guid.NewGuid();
		transform = t;
		model = m;
		ClutterType = Type.Model;
		IsSmall = isSmall;

		// Calculate size from model bounds
		if ( m?.Bounds != null )
		{
			Size = m.Bounds.Size.Length * t.Scale.Length;
		}
		else
		{
			Size = t.Scale.Length; // Fallback to scale
		}
	}

	[Serializable]
	public class ClutterLayer
	{
		public string Name { get; set; } = "New Layer";
		public List<ClutterObject> Objects { get; set; } = new();
		public ClutterComponent Parent { get; private set; }
		public override string ToString() => Name;

		// not serialized
		[Hide]
		public List<ClutterInstance> Instances = [];

		public ClutterLayer( ClutterComponent parent )
		{
			Parent = parent;
		}

		/// <summary>
		/// Returns a random object from the list taking it account the weight of each item
		/// </summary>
		/// <returns></returns>
		public ClutterObject? GetRandomObject()
		{
			return GetWeightedObject( Game.Random );
		}

		/// <summary>
		/// Gets a weighted random object from this layer using a specific random instance
		/// </summary>
		public ClutterObject? GetWeightedObject( Random random )
		{
			if ( Objects.Count == 0 )
				return null;

			var totalWeight = Objects.Sum( o => o.Weight );
			if ( totalWeight > 0 )
			{
				var randomValue = random.Float( 0f, totalWeight );
				float currentWeight = 0f;
				foreach ( var obj in Objects )
				{
					currentWeight += obj.Weight;
					if ( randomValue <= currentWeight )
					{
						return obj;
					}
				}
			}

			// Equal weights fallback
			var index = random.Int( 0, Objects.Count - 1 );
			return Objects[index];
		}

		public void AddInstance( ClutterInstance instance )
		{
			Instances.Add( instance );
		}

		/// <summary>
		/// Create a clutter instance from a ClutterObject with optional resource caching
		/// </summary>
		public static ClutterInstance? CreateInstance( ClutterObject clutterObject, Transform transform, Dictionary<string, object> resourceCache = null )
		{
			return CreateInstance( clutterObject.Path, transform, resourceCache, clutterObject.IsSmall );
		}

		/// <summary>
		/// Create a clutter instance from a path with optional resource caching
		/// </summary>
		public static ClutterInstance? CreateInstance( string path, Transform transform, Dictionary<string, object> resourceCache = null, bool isSmall = false )
		{
			// Try to get from cache first
			if ( resourceCache != null && resourceCache.TryGetValue( path, out var cachedResource ) )
			{
				return CreateInstanceFromResource( cachedResource, transform, isSmall );
			}

			// Load the resource
			object resource = TryLoadPrefab( path ) ?? TryLoadModel( path );

			if ( resource is null )
			{
				Log.Warning( $"Failed to load resource at path: {path}" );
				return null;
			}

			// Cache it
			CacheResource( path, resource, resourceCache );

			// Create instance
			return CreateInstanceFromResource( resource, transform, isSmall );
		}

		private static object TryLoadPrefab( string path )
		{
			// Try prefab if path ends with .prefab or has no extension
			if ( path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
				return null;

			return ResourceLibrary.Get<PrefabFile>( path );
		}

		private static object TryLoadModel( string path )
		{
			// Try model if path ends with .vmdl or has no extension (after prefab failed)
			if ( path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
				return null;

			return Model.Load( path );
		}

		private static void CacheResource( string path, object resource, Dictionary<string, object> resourceCache )
		{
			if ( resourceCache == null )
				return;

			resourceCache[path] = resource;

			// Also cache models by their ResourcePath if different
			if ( resource is Model model && model.ResourcePath != null && model.ResourcePath != path )
			{
				resourceCache[model.ResourcePath] = model;
			}
		}

		private static ClutterInstance? CreateInstanceFromResource( object resource, Transform transform, bool isSmall )
		{
			if ( resource is PrefabFile prefab )
			{
				var prefabScene = SceneUtility.GetPrefabScene( prefab );
				var go = prefabScene.Clone( transform );
				return new ClutterInstance( go, transform, isSmall );
			}

			if ( resource is Model model )
			{
				return new ClutterInstance( model, transform, isSmall );
			}

			return null;
		}
	}

	/// <summary>
	/// Helper class for efficient clutter object creation
	/// </summary>
	public static class ClutterHelper
	{
		/// <summary>
		/// Adds a tracking component to a clutter instance if it's a prefab GameObject
		/// </summary>
		public static void AddTrackerComponent( ClutterInstance? instance )
		{

		}
	}

	[System.Serializable]
	public struct ClutterObject
	{
		public string Path { get; set; }
		public float Weight { get; set; }
		public bool IsSmall { get; set; }

		public ClutterObject( string path, float weight = 0.5f, bool isSmall = false )
		{
			Path = path;
			Weight = weight;
			IsSmall = isSmall;
		}
	}
}
