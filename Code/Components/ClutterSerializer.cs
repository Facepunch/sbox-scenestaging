using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sandbox;

/// <summary>
/// Handles all clutter component serialization and deserialization operations.
/// Manages the compressed storage of model instances using a palette-based approach.
/// </summary>
internal static class ClutterSerializer
{
	/// <summary>
	/// Compressed version using model palette and packed data.
	/// </summary>
	[Serializable]
	struct CompressedModelInstance
	{
		public byte ModelIndex { get; set; }
		public Vector3 Position { get; set; }
		public Vector4 Rotation { get; set; }
		public float Scale { get; set; }
		public Guid VolumeId { get; set; }
		public bool IsSmall { get; set; }
	}

	/// <summary>
	/// Serializes layers from a ClutterSystem to JSON.
	/// </summary>
	public static JsonObject SerializeFromSystem( ClutterSystem clutterSystem )
	{
		var json = new JsonObject();
		var layersArray = new JsonArray();

		// Build instance-to-volume map from ClutterSystem
		var instanceToVolumeMap = BuildInstanceToVolumeMapFromSystem( clutterSystem );

		foreach ( var layer in clutterSystem.GetAllLayers() )
		{
			var layerJson = SerializeLayer( layer, instanceToVolumeMap );
			layersArray.Add( layerJson );
		}

		json["Layers"] = layersArray;

		return json;
	}

	/// <summary>
	/// Builds a map of InstanceId to VolumeId by querying the ClutterSystem
	/// </summary>
	private static Dictionary<Guid, Guid> BuildInstanceToVolumeMapFromSystem( ClutterSystem clutterSystem )
	{
		var map = new Dictionary<Guid, Guid>();

		// Get all volumes in the scene
		var volumes = clutterSystem.Scene.GetAllComponents<ClutterVolumeComponent>();
		foreach ( var volume in volumes )
		{
			var volumeInstances = clutterSystem.GetVolumeInstances( volume.Id );
			foreach ( var instance in volumeInstances )
			{
				map[instance.InstanceId] = volume.Id;
			}
		}

		return map;
	}

	/// <summary>
	/// Serializes a ClutterComponent's layers to JSON, building model palettes for each layer.
	/// Only serializes the compressed layer data - other properties are handled by normal component serialization.
	/// </summary>
	[Obsolete( "Use SerializeFromSystem instead. ClutterComponent is being removed." )]
	public static JsonObject Serialize( ClutterComponent clutterComponent )
	{
		var json = new JsonObject();
		var layersArray = new JsonArray();

		// Build instance-to-volume map from ClutterSystem
		var instanceToVolumeMap = BuildInstanceToVolumeMap( clutterComponent );

		foreach ( var layer in clutterComponent.Layers )
		{
			var layerJson = SerializeLayer( layer, instanceToVolumeMap );
			layersArray.Add( layerJson );
		}

		json["Layers"] = layersArray;

		return json;
	}

	/// <summary>
	/// Builds a map of InstanceId to VolumeId by querying the ClutterSystem
	/// </summary>
	[Obsolete( "Use BuildInstanceToVolumeMapFromSystem instead. ClutterComponent is being removed." )]
	private static Dictionary<Guid, Guid> BuildInstanceToVolumeMap( ClutterComponent clutterComponent )
	{
		var map = new Dictionary<Guid, Guid>();

		var clutterSystem = clutterComponent.Scene?.GetSystem<ClutterSystem>();
		if ( clutterSystem == null )
			return map;

		// Get all volumes in the scene
		var volumes = clutterComponent.Scene.GetAllComponents<ClutterVolumeComponent>();
		foreach ( var volume in volumes )
		{
			var volumeInstances = clutterSystem.GetVolumeInstances( volume.Id );
			foreach ( var instance in volumeInstances )
			{
				map[instance.InstanceId] = volume.Id;
			}
		}

		return map;
	}

	/// <summary>
	/// Serializes a single ClutterLayer to JSON, including its palette and compressed instances.
	/// Builds the model palette and compressed instances on-the-fly from runtime instances.
	/// </summary>
	private static JsonObject SerializeLayer( ClutterLayer layer, Dictionary<Guid, Guid> instanceToVolumeMap )
	{
		JsonObject layerJson = new();

		layerJson["Name"] = layer.Name;

		// Serialize ClutterObjects
		var objectsArray = new JsonArray();
		foreach ( var obj in layer.Objects )
		{
			var objJson = new JsonObject
			{
				["Path"] = obj.Path,
				["Weight"] = obj.Weight,
				["IsSmall"] = obj.IsSmall
			};
			objectsArray.Add( objJson );
		}
		layerJson["Objects"] = objectsArray;

		// Build model palette and compress instances from runtime instances
		var modelPalette = new List<string>();
		var compressedInstances = new List<CompressedModelInstance>();

		foreach ( var instance in layer.Instances )
			{
				// Only serialize model instances
				if ( instance.ClutterType != ClutterInstance.Type.Model || instance.model == null )
					continue;

				// Add model to palette if not present
				if ( !modelPalette.Contains( instance.model.ResourcePath ) )
				{
					if ( modelPalette.Count >= 256 )
					{
						Log.Warning( $"Layer '{layer.Name}' has reached max unique models (256)" );
						continue;
					}
					modelPalette.Add( instance.model.ResourcePath );
				}

				byte modelIndex = (byte)modelPalette.IndexOf( instance.model.ResourcePath );

				// Get VolumeId from the map if available
				Guid volumeId = Guid.Empty;
				instanceToVolumeMap?.TryGetValue( instance.InstanceId, out volumeId );

			compressedInstances.Add( new CompressedModelInstance
			{
				ModelIndex = modelIndex,
				Position = instance.transform.Position,
				Rotation = new Vector4( instance.transform.Rotation.x, instance.transform.Rotation.y, instance.transform.Rotation.z, instance.transform.Rotation.w ),
				Scale = instance.transform.Scale.x,
				VolumeId = volumeId,
				IsSmall = instance.IsSmall
			} );
		}

		// Serialize model palette
		var paletteArray = new JsonArray();
		foreach ( var modelPath in modelPalette )
		{
			paletteArray.Add( modelPath );
		}
		layerJson["ModelPalette"] = paletteArray;

		// Serialize compressed instances
		var instancesArray = new JsonArray();
		foreach ( var instance in compressedInstances )
		{
			var instanceJson = new JsonObject
			{
				["ModelIndex"] = instance.ModelIndex,
				["Position"] = JsonSerializer.SerializeToNode( new { instance.Position.x, instance.Position.y, instance.Position.z } ),
				["Rotation"] = JsonSerializer.SerializeToNode( new { instance.Rotation.x, instance.Rotation.y, instance.Rotation.z, instance.Rotation.w } ),
				["Scale"] = instance.Scale,
				["VolumeId"] = instance.VolumeId.ToString(),
				["IsSmall"] = instance.IsSmall
			};
			instancesArray.Add( instanceJson );
		}
		layerJson["SerializedInstances"] = instancesArray;

		return layerJson;
	}

	/// <summary>
	/// Deserializes JSON into a ClutterSystem, rebuilding all layers and instances.
	/// </summary>
	public static void DeserializeToSystem( ClutterSystem clutterSystem, JsonObject json )
	{
		if ( json.TryGetPropertyValue( "Layers", out var layersNode ) && layersNode is JsonArray layersArray )
		{
			foreach ( var layerNode in layersArray )
			{
				if ( layerNode is JsonObject layerJson )
				{
					var layer = DeserializeLayerForSystem( clutterSystem, layerJson );
					clutterSystem.AddLayer( layer );
				}
			}
		}
	}

	/// <summary>
	/// Deserializes a single ClutterLayer from JSON for the system.
	/// </summary>
	private static ClutterLayer DeserializeLayerForSystem( ClutterSystem clutterSystem, JsonObject layerJson )
	{
		var layer = new ClutterLayer();

		if ( layerJson.TryGetPropertyValue( "Name", out var nameNode ) )
		{
			layer.Name = nameNode.GetValue<string>();
		}

		// Deserialize ClutterObjects
		if ( layerJson.TryGetPropertyValue( "Objects", out var objectsNode ) && objectsNode is JsonArray objectsArray )
		{
			foreach ( var objNode in objectsArray )
			{
				if ( objNode is JsonObject objJson )
				{
					var clutterObject = new ClutterObject
					{
						Path = objJson["Path"]?.GetValue<string>() ?? "",
						Weight = objJson["Weight"]?.GetValue<float>() ?? 0.5f,
						IsSmall = objJson["IsSmall"]?.GetValue<bool>() ?? false
					};
					layer.Objects.Add( clutterObject );
				}
			}
		}

		// Build model palette (used for decompressing instances)
		var modelPalette = new List<string>();
		if ( layerJson.TryGetPropertyValue( "ModelPalette", out var paletteNode ) && paletteNode is JsonArray paletteArray )
		{
			foreach ( var modelPathNode in paletteArray )
			{
				modelPalette.Add( modelPathNode.GetValue<string>() );
			}
		}

		// Build local compressed instances list
		var compressedInstances = new List<CompressedModelInstance>();
		if ( layerJson.TryGetPropertyValue( "SerializedInstances", out var instancesNode ) && instancesNode is JsonArray instancesArray )
		{
			foreach ( var instanceNode in instancesArray )
			{
				if ( instanceNode is JsonObject instanceJson )
				{
					var posNode = instanceJson["Position"];
					var rotNode = instanceJson["Rotation"];

					var instance = new CompressedModelInstance
					{
						ModelIndex = instanceJson["ModelIndex"]?.GetValue<byte>() ?? 0,
						Position = new Vector3(
							posNode["x"]?.GetValue<float>() ?? 0,
							posNode["y"]?.GetValue<float>() ?? 0,
							posNode["z"]?.GetValue<float>() ?? 0
						),
						Rotation = new Vector4(
							rotNode["x"]?.GetValue<float>() ?? 0,
							rotNode["y"]?.GetValue<float>() ?? 0,
							rotNode["z"]?.GetValue<float>() ?? 0,
							rotNode["w"]?.GetValue<float>() ?? 1
						),
						Scale = instanceJson["Scale"]?.GetValue<float>() ?? 1f,
						VolumeId = Guid.Parse( instanceJson["VolumeId"]?.GetValue<string>() ?? Guid.Empty.ToString() ),
						IsSmall = instanceJson["IsSmall"]?.GetValue<bool>() ?? false
					};
					compressedInstances.Add( instance );
				}
			}
		}

		// Rebuild runtime instances from compressed data
		RebuildLayerRuntimeInstances( layer, modelPalette, compressedInstances );

		return layer;
	}

	/// <summary>
	/// Deserializes JSON into a ClutterComponent, rebuilding all layers and instances.
	/// Only deserializes the compressed layer data - other properties are handled by normal component serialization.
	/// </summary>
	[Obsolete( "Use DeserializeToSystem instead. ClutterComponent is being removed." )]
	public static void Deserialize( ClutterComponent clutterComponent, JsonObject json )
	{
		clutterComponent.Layers.Clear();

		if ( json.TryGetPropertyValue( "Layers", out var layersNode ) && layersNode is JsonArray layersArray )
		{
			foreach ( var layerNode in layersArray )
			{
				if ( layerNode is JsonObject layerJson )
				{
					var layer = DeserializeLayer( clutterComponent, layerJson );
					clutterComponent.Layers.Add( layer );
				}
			}
		}
	}

	/// <summary>
	/// Deserializes a single ClutterLayer from JSON.
	/// </summary>
	[Obsolete( "Use DeserializeLayerForSystem instead. ClutterComponent is being removed." )]
	private static ClutterLayer DeserializeLayer( ClutterComponent clutterComponent, JsonObject layerJson )
	{
		var layer = new ClutterLayer();

		if ( layerJson.TryGetPropertyValue( "Name", out var nameNode ) )
		{
			layer.Name = nameNode.GetValue<string>();
		}

		// Deserialize ClutterObjects
		if ( layerJson.TryGetPropertyValue( "Objects", out var objectsNode ) && objectsNode is JsonArray objectsArray )
		{
			foreach ( var objNode in objectsArray )
			{
				if ( objNode is JsonObject objJson )
				{
					var clutterObject = new ClutterObject
					{
						Path = objJson["Path"]?.GetValue<string>() ?? "",
						Weight = objJson["Weight"]?.GetValue<float>() ?? 0.5f,
						IsSmall = objJson["IsSmall"]?.GetValue<bool>() ?? false
					};
					layer.Objects.Add( clutterObject );
				}
			}
		}

		// Build model palette (used for decompressing instances)
		var modelPalette = new List<string>();
		if ( layerJson.TryGetPropertyValue( "ModelPalette", out var paletteNode ) && paletteNode is JsonArray paletteArray )
		{
			foreach ( var modelPathNode in paletteArray )
			{
				modelPalette.Add( modelPathNode.GetValue<string>() );
			}
		}

		// Build local compressed instances list
		var compressedInstances = new List<CompressedModelInstance>();
		if ( layerJson.TryGetPropertyValue( "SerializedInstances", out var instancesNode ) && instancesNode is JsonArray instancesArray )
		{
			foreach ( var instanceNode in instancesArray )
			{
				if ( instanceNode is JsonObject instanceJson )
				{
					var posNode = instanceJson["Position"];
					var rotNode = instanceJson["Rotation"];

					var instance = new CompressedModelInstance
					{
						ModelIndex = instanceJson["ModelIndex"]?.GetValue<byte>() ?? 0,
						Position = new Vector3(
							posNode["x"]?.GetValue<float>() ?? 0,
							posNode["y"]?.GetValue<float>() ?? 0,
							posNode["z"]?.GetValue<float>() ?? 0
						),
						Rotation = new Vector4(
							rotNode["x"]?.GetValue<float>() ?? 0,
							rotNode["y"]?.GetValue<float>() ?? 0,
							rotNode["z"]?.GetValue<float>() ?? 0,
							rotNode["w"]?.GetValue<float>() ?? 1
						),
						Scale = instanceJson["Scale"]?.GetValue<float>() ?? 1f,
						VolumeId = Guid.Parse( instanceJson["VolumeId"]?.GetValue<string>() ?? Guid.Empty.ToString() ),
						IsSmall = instanceJson["IsSmall"]?.GetValue<bool>() ?? false
					};
					compressedInstances.Add( instance );
				}
			}
		}

		// Rebuild runtime instances from compressed data
		RebuildLayerRuntimeInstances( layer, modelPalette, compressedInstances );

		return layer;
	}

	/// <summary>
	/// Rebuilds runtime instances from serialized data for a single layer.
	/// </summary>
	private static void RebuildLayerRuntimeInstances( ClutterLayer layer, List<string> modelPalette, List<CompressedModelInstance> compressedInstances )
	{
		layer.Instances.Clear();

		if ( compressedInstances.Count == 0 )
			return;

		// Preload all unique models from palette (much faster than loading per-instance)
		var modelCache = new Dictionary<byte, Model>();
		for ( byte i = 0; i < modelPalette.Count; i++ )
		{
			var modelPath = modelPalette[i];
			var model = Model.Load( modelPath );
			if ( model != null )
			{
				modelCache[i] = model;
			}
			else
			{
				Log.Warning( $"Failed to load model: {modelPath}" );
			}
		}

		// Create runtime instances using cached models
		foreach ( var compressed in compressedInstances )
		{
			// Get model from cache
			if ( !modelCache.TryGetValue( compressed.ModelIndex, out var model ) )
			{
				continue;
			}

			var transform = new Transform(
				compressed.Position,
				new Rotation( compressed.Rotation.x, compressed.Rotation.y, compressed.Rotation.z, compressed.Rotation.w ),
				compressed.Scale
			);

			var instance = new ClutterInstance( model, transform, compressed.IsSmall );
			layer.Instances.Add( instance );
		}
	}

	/// <summary>
	/// Builds a mapping of instance positions to VolumeIds from scene metadata for the system.
	/// This is used to rebuild volume instance lists after scene load.
	/// </summary>
	public static Dictionary<Vector3, Guid> GetInstanceVolumeMappingFromSystem( ClutterSystem clutterSystem )
	{
		var mapping = new Dictionary<Vector3, Guid>();

		// Get metadata from scene
		var sceneFile = clutterSystem.Scene?.Source as SceneFile;
		if ( sceneFile?.SceneProperties == null )
			return mapping;

		try
		{
			// Navigate to metadata
			if ( !sceneFile.SceneProperties.TryGetPropertyValue( "Metadata", out var metadataNode ) || metadataNode is not JsonObject metadata )
				return mapping;

			// Get ClutterSystem data
			if ( !metadata.TryGetPropertyValue( "ClutterSystem_data", out var dataNode ) )
				return mapping;

			var dataString = dataNode?.GetValue<string>();
			if ( string.IsNullOrEmpty( dataString ) )
				return mapping;

			// Parse the clutter data
			var json = JsonNode.Parse( dataString ) as JsonObject;
			if ( json == null )
				return mapping;

			if ( json.TryGetPropertyValue( "Layers", out var layersNode ) && layersNode is JsonArray layersArray )
			{
				foreach ( var layerNode in layersArray )
				{
					if ( layerNode is JsonObject layerJson )
					{
						// Parse compressed instances to get VolumeId mappings
						if ( layerJson.TryGetPropertyValue( "SerializedInstances", out var instancesNode ) && instancesNode is JsonArray instancesArray )
						{
							foreach ( var instanceNode in instancesArray )
							{
								if ( instanceNode is JsonObject instanceJson )
								{
									var posNode = instanceJson["Position"];
									var volumeIdStr = instanceJson["VolumeId"]?.GetValue<string>();

									if ( posNode != null && !string.IsNullOrEmpty( volumeIdStr ) )
									{
										var position = new Vector3(
											posNode["x"]?.GetValue<float>() ?? 0,
											posNode["y"]?.GetValue<float>() ?? 0,
											posNode["z"]?.GetValue<float>() ?? 0
										);

										var volumeId = Guid.Parse( volumeIdStr );
										if ( volumeId != Guid.Empty )
										{
											mapping[position] = volumeId;
										}
									}
								}
							}
						}
					}
				}
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to parse instance-volume mapping: {ex.Message}" );
		}

		return mapping;
	}

	/// <summary>
	/// Builds a mapping of instance positions to VolumeIds from scene metadata.
	/// This is used to rebuild volume instance lists after scene load.
	/// </summary>
	[Obsolete( "Use GetInstanceVolumeMappingFromSystem instead. ClutterComponent is being removed." )]
	public static Dictionary<Vector3, Guid> GetInstanceVolumeMapping( ClutterComponent clutterComponent )
	{
		var mapping = new Dictionary<Vector3, Guid>();

		// Get metadata from scene
		var sceneFile = clutterComponent.Scene?.Source as SceneFile;
		if ( sceneFile?.SceneProperties == null )
			return mapping;

		try
		{
			// Navigate to metadata
			if ( !sceneFile.SceneProperties.TryGetPropertyValue( "Metadata", out var metadataNode ) || metadataNode is not JsonObject metadata )
				return mapping;

			// Get ClutterSystem data
			if ( !metadata.TryGetPropertyValue( "ClutterSystem_data", out var dataNode ) )
				return mapping;

			var dataString = dataNode?.GetValue<string>();
			if ( string.IsNullOrEmpty( dataString ) )
				return mapping;

			// Parse the clutter data
			var json = JsonNode.Parse( dataString ) as JsonObject;
			if ( json == null )
				return mapping;

			if ( json.TryGetPropertyValue( "Layers", out var layersNode ) && layersNode is JsonArray layersArray )
			{
				foreach ( var layerNode in layersArray )
				{
					if ( layerNode is JsonObject layerJson )
					{
						// Parse compressed instances to get VolumeId mappings
						if ( layerJson.TryGetPropertyValue( "SerializedInstances", out var instancesNode ) && instancesNode is JsonArray instancesArray )
						{
							foreach ( var instanceNode in instancesArray )
							{
								if ( instanceNode is JsonObject instanceJson )
								{
									var posNode = instanceJson["Position"];
									var volumeIdStr = instanceJson["VolumeId"]?.GetValue<string>();

									if ( posNode != null && !string.IsNullOrEmpty( volumeIdStr ) )
									{
										var position = new Vector3(
											posNode["x"]?.GetValue<float>() ?? 0,
											posNode["y"]?.GetValue<float>() ?? 0,
											posNode["z"]?.GetValue<float>() ?? 0
										);

										var volumeId = Guid.Parse( volumeIdStr );
										if ( volumeId != Guid.Empty )
										{
											mapping[position] = volumeId;
										}
									}
								}
							}
						}
					}
				}
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to parse instance-volume mapping: {ex.Message}" );
		}

		return mapping;
	}
}
