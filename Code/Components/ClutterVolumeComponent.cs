using System.Text.Json.Nodes;

namespace Sandbox;

/// <summary>
/// Represents a volume which can procedurally scatter clutter within its bounds.
/// This is a data-only component - scattering logic is handled by editor tools.
/// </summary>
public sealed class ClutterVolumeComponent : Component, Component.ExecuteInEditor
{
	[Property, Hide]
	public string ScattererTypeName { get; set; }

	/// <summary>
	/// Used for dropdown, since we are setting them by type name
	/// </summary>
	[Property, Title( "Scatterer Type" ), Editor( "scatterer_type" )]
	public string ScattererName
	{
		get => Scatterer?.GetType().Name ?? "";
		set
		{
			if ( string.IsNullOrEmpty( value ) )
			{
				_scatterer = null;
				_settingsHaveBeenLoaded = false;
				SerializeScattererSettings();
				return;
			}

			var scattererType = Game.TypeLibrary.GetTypes<ScattererBase>()
				.FirstOrDefault( t => !t.IsAbstract && !t.IsInterface && t.Name == value );

			if ( scattererType != null )
			{
				_scatterer = scattererType.Create<ScattererBase>();
				_settingsHaveBeenLoaded = false; // Reset flag when type changes
			}
		}
	}

	private bool _isDeserializing = false;
	private bool _settingsHaveBeenLoaded = false;

	/// <summary>
	///  The procedural scatterer to use for this volume.
	/// </summary>
	private ScattererBase _scatterer;

	[Property]
	public ScattererBase Scatterer
	{
		get => _scatterer;
		set
		{
			_scatterer = value;

			if ( !_isDeserializing )
			{
				SerializeScattererSettings();
			}
		}
	}

	/// <summary>
	/// Serialized settings for the procedural scatterer, this can store any data the scatterer wants
	/// </summary>
	[Property, Hide]
	public JsonObject ScattererSettings { get; set; }

	/// <summary>
	/// Serializes the current scatterer settings to properties
	/// </summary>
	public void SerializeScattererSettings()
	{
		if ( _scatterer == null )
		{
			ScattererTypeName = null;
			ScattererSettings = null;
			return;
		}

		try
		{
			// Store the type name
			var scattererType = _scatterer.GetType();
			ScattererTypeName = scattererType.FullName;
			ScattererSettings = _scatterer.Serialize();
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"[{GameObject?.Name ?? "ClutterVolume"}] Failed to serialize scatterer settings: {ex.Message}" );
			ScattererTypeName = null;
			ScattererSettings = null;
		}
	}

	/// <summary>
	/// Deserializes and applies scatterer settings from properties
	/// </summary>
	private void DeserializeScattererSettings()
	{
		if ( string.IsNullOrEmpty( ScattererTypeName ) || ScattererSettings == null || _scatterer == null )
			return;

		if ( _settingsHaveBeenLoaded )
		{
			return;
		}

		_isDeserializing = true;
		try
		{
			var currentTypeName = _scatterer.GetType().FullName;

			// Only restore if types match
			if ( ScattererTypeName != currentTypeName )
			{
				ScattererTypeName = null;
				ScattererSettings = null;
				return;
			}

			// Use the scatterer's own deserialization method
			_scatterer.Deserialize( ScattererSettings );

			_settingsHaveBeenLoaded = true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[{GameObject?.Name ?? "ClutterVolume"}] Failed to deserialize scatterer settings: {ex.Message}\n{ex.StackTrace}" );
		}
		finally
		{
			_isDeserializing = false;
		}
	}

	/// <summary>
	/// Ensures scatterer settings have been loaded (used before opening settings dialog)
	/// </summary>
	public void EnsureScattererSettingsLoaded()
	{
		if ( !string.IsNullOrEmpty( ScattererTypeName ) && ScattererSettings != null && _scatterer != null )
		{
			DeserializeScattererSettings();
		}
	}

	/// <summary>
	/// Optional scatter brush resource to use for this volume
	/// </summary>
	[Property, Group( "Source" )]
	public ScatterBrush Brush { get; set; }

	/// <summary>
	/// Selected layers for this volume (only shown when no brush is assigned)
	/// </summary>
	[Property, Editor( "clutter_layer_multi" ), Group( "Source" ), Title( "Layers" ), ShowIf( nameof( ShouldShowLayerSelection ), true )]
	public List<ClutterLayer> SelectedLayers
	{
		get
		{
			// Reconstruct from IDs
			if ( _selectedLayerIds?.Count > 0 )
			{
				var allLayers = GetAllSceneLayers();
				return allLayers.Where( l => _selectedLayerIds.Contains( l.Id ) ).ToList();
			}
			return new List<ClutterLayer>();
		}
		set
		{
			// Store as IDs
			_selectedLayerIds = value?.Select( l => l.Id ).ToList() ?? [];
		}
	}

	/// <summary>
	/// Internal storage - IDs of selected layers (stable across renames)
	/// </summary>
	[Property, Hide]
	private List<Guid> _selectedLayerIds { get; set; } = [];

	/// <summary>
	/// Only show layer selection if no brush is assigned
	/// </summary>
	private bool ShouldShowLayerSelection => Brush == null;

	/// <summary>
	/// Gets all layers from the ClutterSystem
	/// </summary>
	private List<ClutterLayer> GetAllSceneLayers()
	{
		var clutterSystem = Scene?.GetSystem<ClutterSystem>();
		if ( clutterSystem != null )
		{
			return clutterSystem.GetAllLayers().ToList();
		}
		return new List<ClutterLayer>();
	}

	/// <summary>
	/// Gets the active layers for this volume based on selection
	/// </summary>
	public List<ClutterLayer> GetActiveLayers()
	{
		// If a brush is assigned, use its layers
		if ( Brush != null && Brush.Layers?.Count > 0 )
		{
			return Brush.Layers;
		}

		var allLayers = GetAllSceneLayers();

		// If specific layers are selected by ID, use them (if they exist)
		if ( _selectedLayerIds?.Count > 0 )
		{
			return allLayers.Where( l => _selectedLayerIds.Contains( l.Id ) ).ToList();
		}

		// If no valid selected layers, fall back to all layers from all components
		return allLayers;
	}

	[Property]
	[Range( 0, 1 )]
	public float Density { get; set; } = 0.5f;

	/// <summary>
	/// The size of the box, from corner to corner.
	/// </summary>
	[Property, Title( "Size" ), Group( "Box" )]
	public Vector3 Scale { get; set; }

	/// <summary>
	/// The center of the box relative to this GameObject
	/// </summary>
	[Property, Group( "Box" )]
	public Vector3 Center { get; set; }

	protected override void OnEnabled()
	{
		// Restore scatterer settings
		if ( !string.IsNullOrEmpty( ScattererTypeName ) && ScattererSettings != null && _scatterer != null )
		{
			DeserializeScattererSettings();
		}
	}

	public BBox GetScatterBounds()
	{
		return BBox.FromPositionAndSize( Transform.World.PointToWorld( Center ), Scale );
	}

	/// <summary>
	/// Gets or creates a parent GameObject for the specified layer.
	/// Layer parent GameObjects are children of this volume's GameObject.
	/// </summary>
	public GameObject GetOrCreateLayerParent( ClutterLayer layer )
	{
		// Look for existing layer parent by name
		var existingParent = GameObject.Children.FirstOrDefault( c => c.Name == layer.Name );
		if ( existingParent != null )
		{
			return existingParent;
		}

		// Create new layer parent GameObject
		var parentGameObject = Scene.CreateObject();
		parentGameObject.Name = layer.Name;
		parentGameObject.SetParent( GameObject, false );
		parentGameObject.Tags.Add( "clutter_layer" );

		return parentGameObject;
	}

	/// <summary>
	/// Draw the BBox gizmo of the volume in the editor
	/// </summary>
	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected && !Gizmo.IsHovered )
			return;

		var currentBox = BBox.FromPositionAndSize( Center, Scale );

		using ( Gizmo.Scope( "Clutter Volume Editor" ) )
		{
			if ( Gizmo.Control.BoundingBox( "Bounds", currentBox, out var newBox ) )
			{
				Center = newBox.Center;
				Scale = newBox.Size;
			}

			Gizmo.Draw.LineThickness = 1;
			Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
			Gizmo.Draw.LineBBox( currentBox );
		}
	}
}
