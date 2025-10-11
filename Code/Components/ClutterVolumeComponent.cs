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
	/// Serialized settings for the procedural scatterer
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
		catch ( System.Exception ex )
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

	[Property, Editor( "clutter_layer_multi" )]
	public List<ClutterLayer> SelectedLayers
	{
		get
		{
			if ( SelectedLayerNames?.Count > 0 )
			{
				var allLayers = GetAllSceneLayers();
				_selectedLayers = allLayers
					.Where( l => SelectedLayerNames.Contains( l.Name ) )
					.ToList();
			}
			return _selectedLayers;
		}
		set
		{
			_selectedLayers = value;
			SelectedLayerNames = value?.Select( l => l.Name ).ToList() ?? [];
		}
	}
	private List<ClutterLayer> _selectedLayers = [];

	// Serialized layer names for persistence
	[Property, Hide]
	public List<string> SelectedLayerNames { get; set; } = [];

	/// <summary>
	/// Gets all layers from all ClutterComponents in the scene
	/// </summary>
	private List<ClutterLayer> GetAllSceneLayers()
	{
		var allLayers = new List<ClutterLayer>();
		var clutterComponents = Scene.GetAllComponents<ClutterComponent>();
		foreach ( var component in clutterComponents )
		{
			allLayers.AddRange( component.Layers );
		}
		return allLayers;
	}

	/// <summary>
	/// Gets the active layers for this volume based on selection
	/// </summary>
	public List<ClutterLayer> GetActiveLayers()
	{
		var allLayers = GetAllSceneLayers();

		// If specific layers are selected, use them (if they exist)
		if ( SelectedLayers?.Count > 0 )
		{
			return SelectedLayers.Where( l => allLayers.Contains( l ) ).ToList();
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
