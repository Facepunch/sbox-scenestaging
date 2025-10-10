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
		/// Returns a random object from the list taking it account the weigth of each tiem
		/// </summary>
		/// <returns></returns>
		public ClutterObject? GetRandomObject()
		{
			if ( Objects.Count == 0 )
				return null;

			// Calculate total weight
			float totalWeight = 0f;
			foreach ( var clutterObject in Objects )
			{
				totalWeight += clutterObject.Weight;
			}

			if ( totalWeight <= 0f )
			{
				// Fallback to equal weights if all weights are 0
				var randomIndex = Game.Random.Int( 0, Objects.Count - 1 );
				var randomClutterObject = Objects[randomIndex];
				return randomClutterObject;
			}

			// Generate random number between 0 and total weight
			float randomValue = Game.Random.Float( 0f, totalWeight );

			// Find the object corresponding to this weight
			float currentWeight = 0f;
			foreach ( var clutterObject in Objects )
			{
				currentWeight += clutterObject.Weight;
				if ( randomValue <= currentWeight )
				{
					return clutterObject;
				}
			}

			// Fallback (should never reach here)
			return Objects.Last();
		}

		public void AddInstance( ClutterInstance instance )
		{
			Instances.Add( instance );
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

		/// <summary>
		/// Gets a weighted random object from a layer using a specific random instance
		/// </summary>
		public static ClutterObject? GetWeightedObject( ClutterLayer layer, Random random )
		{
			if ( layer.Objects.Count == 0 )
				return null;

			var totalWeight = layer.Objects.Sum( o => o.Weight );
			if ( totalWeight > 0 )
			{
				var randomValue = random.Float( 0f, totalWeight );
				float currentWeight = 0f;
				foreach ( var obj in layer.Objects )
				{
					currentWeight += obj.Weight;
					if ( randomValue <= currentWeight )
					{
						return obj;
					}
				}
			}

			// Equal weights fallback
			var index = random.Int( 0, layer.Objects.Count - 1 );
			return layer.Objects[index];
		}

		/// <summary>
		/// Create objects efficiently using cached resources from a ClutterObject
		/// </summary>
		public static ClutterInstance? CreateClutterObject( ClutterObject clutterObject, Transform transform, Dictionary<string, object> resourceCache = null )
		{
			return CreateClutterObject( clutterObject.Path, transform, resourceCache, clutterObject.IsSmall );
		}

		/// <summary>
		/// Create objects efficiently using cached resources from a path
		/// </summary>
		public static ClutterInstance? CreateClutterObject( string path, Transform transform, Dictionary<string, object> resourceCache = null, bool isSmall = false )
		{
			// Use cache if available
			if ( resourceCache != null && resourceCache.TryGetValue( path, out var cachedResource ) )
			{
				if ( cachedResource is PrefabFile prefab )
				{
					var prefabScene = SceneUtility.GetPrefabScene( prefab );
					var go = prefabScene.Clone( transform );
					return new ClutterInstance( go, transform, isSmall );
				}
				else if ( cachedResource is Model model )
				{
					return new ClutterInstance( model, transform, isSmall );
				}
			}
			else if ( resourceCache != null )
			{
				// Log cache miss for debugging
				Log.Info( $"Cache miss for: {path}. Cache has {resourceCache.Count} entries" );
				if ( resourceCache.Count > 0 && resourceCache.Count < 20 )
				{
					Log.Info( $"Cache keys: {string.Join( ", ", resourceCache.Keys )}" );
				}
			}
			else
			{
				// Fallback to loading directly - check file extension to determine type
				if ( path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
				{
					var prefab = ResourceLibrary.Get<PrefabFile>( path );
					if ( prefab != null )
					{
						// Add to cache if cache is provided
						if ( resourceCache != null )
							resourceCache[path] = prefab;

						var prefabScene = SceneUtility.GetPrefabScene( prefab );
						var go = prefabScene.Clone( transform );
						return new ClutterInstance( go, transform, isSmall );
					}
					else
					{
						Log.Warning( $"Failed to load prefab: {path}" );
					}
				}
				else if ( path.EndsWith( ".vmdl", StringComparison.OrdinalIgnoreCase ) )
				{
					// Try loading the model
					var model = ResourceLibrary.Get<Model>( path );

					if ( model != null )
					{
						// Add to cache if cache is provided
						if ( resourceCache != null )
						{
							resourceCache[path] = model;
							// Also cache by ResourcePath if different
							if ( model.ResourcePath != null && model.ResourcePath != path )
							{
								resourceCache[model.ResourcePath] = model;
							}
						}

						return new ClutterInstance( model, transform, isSmall );
					}
					else
					{
						Log.Warning( $"Failed to load model: {path}" );
					}
				}
				else
				{
					// Try both types if extension is unclear
					var prefab = ResourceLibrary.Get<PrefabFile>( path );
					if ( prefab != null )
					{
						// Add to cache if cache is provided
						if ( resourceCache != null )
							resourceCache[path] = prefab;

						var prefabScene = SceneUtility.GetPrefabScene( prefab );
						var go = prefabScene.Clone( transform );
						return new ClutterInstance( go, transform, isSmall );
					}

					var model = ResourceLibrary.Get<Model>( path );
					if ( model != null )
					{
						// Add to cache if cache is provided
						if ( resourceCache != null )
							resourceCache[path] = model;

						return new ClutterInstance( model, transform, isSmall );
					}
				}
			}

			// Failed to load resource
			Log.Warning( $"ClutterHelper.CreateClutterObject failed to load resource at path: {path}" );
			return null;
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
