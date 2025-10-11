using System.Text.Json.Nodes;
using static Sandbox.ClutterInstance;

namespace Sandbox;

/// <summary>
/// Clutter component for organizing and managing scattered objects
/// </summary>
public sealed class ClutterComponent : Component, Component.ExecuteInEditor
{
	[Property] public List<ClutterLayer> Layers { get; set; } = [];

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
				if ( JsonNode.Parse( SerializedData ) is JsonObject json )
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
			layer.Instances.RemoveAll( i => i.InstanceId == instance.InstanceId );
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
		public List<ClutterObject> Objects { get; set; } = [];
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
			if ( Objects.Count == 0 )
				return null;

			var totalWeight = Objects.Sum( o => o.Weight );
			if ( totalWeight == 0 )
			{
				var index = Game.Random.Int( 0, Objects.Count - 1 );
				return Objects[index];
			}

			var randomValue = Game.Random.Float( 0f, totalWeight );
			float currentWeight = 0f;
			foreach ( var obj in Objects )
			{
				currentWeight += obj.Weight;
				if ( randomValue <= currentWeight )
				{
					return obj;
				}
			}

			return null;
		}


		public void AddInstance( ClutterInstance instance )
		{
			Instances.Add( instance );
		}
	}

	[Serializable]
	public struct ClutterObject( string path, float weight = 0.5f, bool isSmall = false )
	{
		public string Path { get; set; } = path;
		public float Weight { get; set; } = weight;
		public bool IsSmall { get; set; } = isSmall;
	}
}
