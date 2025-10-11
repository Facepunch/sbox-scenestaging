using System.Text.Json.Nodes;

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
	public static void RemoveInstanceFromLayers( ClutterInstance instance, List<ClutterLayer> layers )
	{
		foreach ( var layer in layers )
		{
			layer.Instances.RemoveAll( i => i.InstanceId == instance.InstanceId );
		}
	}
}
