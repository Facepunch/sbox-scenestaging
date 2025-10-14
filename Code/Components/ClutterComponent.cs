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

	protected override void OnEnabled() => DeserializeData();
	protected override void OnDestroy() => SerializeData();

	/// <summary>
	/// Serializes the current state to the SerializedData property
	/// </summary>
	public void SerializeData()
	{
		var json = ClutterSerializer.Serialize( this );
		SerializedData = json.ToJsonString();
	}

	/// <summary>
	/// Deserializes the clutter data
	/// </summary>
	private void DeserializeData()
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
}
