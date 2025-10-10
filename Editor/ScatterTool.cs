using Editor.TerrainEditor;
using Sandbox;
using System;
using static Sandbox.ClutterInstance;

namespace Editor.TerrainTool;

public class InspectorSettings
{
	private const string CookiePrefix = "ScatterTool.";

	public int BrushSize
	{
		get => EditorCookie.Get( CookiePrefix + "BrushSize", 1 );
		set => EditorCookie.Set( CookiePrefix + "BrushSize", value );
	}

	[Range( 0f, 1f )]
	public float BrushOpacity
	{
		get => EditorCookie.Get( CookiePrefix + "BrushOpacity", 1f );
		set => EditorCookie.Set( CookiePrefix + "BrushOpacity", value );
	}
	public bool EraseMode { get; set; } = false;

	// Clutter system integration
	[Editor( "clutter_layer_multi" )]
	public List<ClutterLayer> SelectedClutterLayers { get; set; } = [];

	// Procedural scatterer selection (like in ClutterVolumeComponent)
	[Title( "Scatterer Type" ), Editor( "scatterer_type" )]
	public string ScattererName
	{
		get => Scatterer?.GetType().Name ?? "";
		set
		{
			if ( string.IsNullOrEmpty( value ) )
			{
				_scatterer = null;
				return;
			}

			var scattererType = Game.TypeLibrary.GetTypes<IProceduralScatterer>()
				.FirstOrDefault( t => !t.IsAbstract && !t.IsInterface && t.Name == value );

			if ( scattererType != null )
			{
				_scatterer = scattererType.Create<IProceduralScatterer>();

				// Try to load saved settings for this scatterer type
				LoadScattererSettings();

				EditorCookie.Set( CookiePrefix + "ScattererType", value );
			}
		}
	}

	[Property]
	public IProceduralScatterer Scatterer
	{
		get => _scatterer;
		set
		{
			_scatterer = value;
			SaveScattererSettings();
		}
	}

	private IProceduralScatterer _scatterer;

	public InspectorSettings()
	{
		// Load scatterer from cookie
		var savedScattererType = EditorCookie.Get<string>( CookiePrefix + "ScattererType", null );
		if ( !string.IsNullOrEmpty( savedScattererType ) )
		{
			var scattererType = Game.TypeLibrary.GetTypes<IProceduralScatterer>()
				.FirstOrDefault( t => !t.IsAbstract && !t.IsInterface && t.Name == savedScattererType );

			_scatterer = scattererType?.Create<IProceduralScatterer>() ?? new DefaultScatterer();
			LoadScattererSettings();
		}
		else
		{
			_scatterer = new DefaultScatterer();
		}
	}

	public void SaveScattererSettings()
	{
		if ( _scatterer == null ) return;

		var typeName = _scatterer.GetType().Name;
		var settings = _scatterer.Serialize();
		EditorCookie.Set( CookiePrefix + "Scatterer." + typeName, settings );
	}

	private void LoadScattererSettings()
	{
		if ( _scatterer == null ) return;

		var typeName = _scatterer.GetType().Name;
		var settings = EditorCookie.Get<System.Text.Json.Nodes.JsonObject>( CookiePrefix + "Scatterer." + typeName, null );
		if ( settings != null )
		{
			_scatterer.Deserialize( settings );
		}
	}
}

[EditorTool( "terrain.scatter" )]
[Title( "Scatter Tool" )]
[Icon( "forest" )]
public sealed class ScatterTool : EditorTool
{
	private bool _dragging = false;
	internal InspectorSettings _settings = new();
	private float _lastScatterTime = 0f;
	private IDisposable _undoScope;
	private ScatterToolOverlay _overlay;
	private Dictionary<string, object> _globalResourceCache = new();
	private Dictionary<string, BBox> _assetBoundsCache = new();

	public override void OnEnabled()
	{
		// Create and show overlay
		_overlay = new ScatterToolOverlay( this );
		AddOverlay( _overlay, TextFlag.RightBottom, 10 );
	}

	public override void OnDisabled()
	{
		_overlay?.Close();
		_overlay = null;

		// Clean up brush preview
		_previewObject?.Delete();
		_previewObject = null;

		// Clear caches to prevent memory leaks
		_globalResourceCache.Clear();
		_assetBoundsCache.Clear();

		// Save scatterer settings
		_settings.SaveScattererSettings();
	}

	public override void OnUpdate()
	{
		// Temporarily toggle erase mode when Ctrl is held (like terrain tool)
		var ctlrHeld = Gizmo.IsCtrlPressed;
		if ( Gizmo.IsCtrlPressed && !_settings.EraseMode )
		{
			_settings.EraseMode = true;
		}

		// Draw brush preview - this also captures mouse events to prevent selection
		DrawBrushPreview();

		// Create an invisible hitbox to capture all mouse events and prevent entity selection
		Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 999999 ) );

		// Use the gizmo system to capture mouse events and prevent object selection
		if ( Gizmo.IsLeftMouseDown )
		{
			bool shouldSculpt = !_dragging || !Application.CursorDelta.IsNearZeroLength;

			if ( shouldSculpt )
			{
				_dragging = true;
				OnPaintBegin();
			}

			OnPaintUpdate();
		}
		else if ( _dragging )
		{
			_dragging = false;
			OnPaintEnded();
		}

		// Restore erase mode after ctrl is released
		if ( !ctlrHeld )
		{
			_settings.EraseMode = false;
		}
	}

	BrushPreviewSceneObject _previewObject;

	void DrawBrushPreview()
	{
		var tr = Trace.UseRenderMeshes( true ).WithTag( "solid" ).WithoutTags( "scattered_object" ).Run();
		if ( !tr.Hit )
			return;

		_previewObject ??= new BrushPreviewSceneObject( Gizmo.World );

		var brushRadius = _settings.BrushSize * 50f;

		// Set brush preview style - different colors for scatter vs erase
		var color = _settings.EraseMode ? Color.FromBytes( 250, 150, 150 ) : Color.FromBytes( 150, 150, 250 );
		color.a = _settings.BrushOpacity;

		// Get the selected brush texture from the terrain system
		var brush = Editor.TerrainEditor.TerrainEditorTool.Brush;

		// Position slightly above the surface to avoid z-fighting
		var previewPosition = tr.HitPosition + tr.Normal * 1f;

		// Create rotation to align circle with surface normal
		var surfaceRotation = Rotation.LookAt( tr.Normal );

		_previewObject.RenderLayer = SceneRenderLayer.OverlayWithDepth;
		_previewObject.Bounds = BBox.FromPositionAndSize( 0, float.MaxValue );
		_previewObject.Transform = new Transform( previewPosition, surfaceRotation );
		_previewObject.Radius = brushRadius;
		_previewObject.Texture = brush?.Texture;
		_previewObject.Color = color;
	}

	void OnPaintBegin()
	{
		var operationName = _settings.EraseMode ? "Erase Objects" : "Scatter Objects";
		_undoScope ??= SceneEditorSession.Active.UndoScope( operationName ).WithGameObjectCreations().Push();
	}

	void OnPaintUpdate()
	{
		// Use opacity to control scatter/erase rate
		var actionInterval = 1.0f / Math.Max( 0.1f, _settings.BrushOpacity * 10f );
		var currentTime = Time.Now;

		if ( currentTime - _lastScatterTime < actionInterval )
			return;

		_lastScatterTime = currentTime;

		var tr = Trace.UseRenderMeshes( true ).WithTag( "solid" ).WithoutTags( "scattered_object" ).Run();
		if ( !tr.Hit )
			return;

		var brushRadius = _settings.BrushSize * 50f;
		var brushCenter = tr.HitPosition;

		// Handle erasing mode
		if ( _settings.EraseMode )
		{
			// Get layers to erase from
			var layersToErase = new List<ClutterLayer>();

			if ( _settings.SelectedClutterLayers != null && _settings.SelectedClutterLayers.Count > 0 )
			{
				// Specific layers selected - erase only from those layers
				layersToErase.AddRange( _settings.SelectedClutterLayers );
			}
			else
			{
				// Nothing selected - erase from all layers in all components
				var allComponents = Scene.GetAllComponents<ClutterComponent>();
				foreach ( var component in allComponents )
				{
					layersToErase.AddRange( component.Layers );
				}
			}

			if ( layersToErase.Count > 0 )
			{
				new ClutterScatterer( Scene )
					.WithBrush( brushCenter, brushRadius, _settings.BrushOpacity )
					.WithErase( true )
					.WithLayers( layersToErase )
					.Run();
			}
			return;
		}

		// Scattering mode - requires at least one selected layer
		if ( _settings.SelectedClutterLayers == null || _settings.SelectedClutterLayers.Count == 0 )
		{
			Log.Warning( "Please select at least one Clutter Layer to scatter objects" );
			return;
		}

		var layersToUse = _settings.SelectedClutterLayers;

		// Use ClutterScatterer API
		var scatterer = new ClutterScatterer( Scene )
			.WithBrush( brushCenter, brushRadius, _settings.BrushOpacity )
			.WithLayers( layersToUse );

		// Add procedural scatterer if available
		if ( _settings.Scatterer != null )
		{
			scatterer.WithScatterer( _settings.Scatterer );
		}

		scatterer.Run();

		// Serialize the components that own these layers (brush mode needs manual serialization)
		var serializedComponents = new HashSet<ClutterComponent>();
		foreach ( var layer in layersToUse )
		{
			var owningComponent = GetComponentForLayer( layer );
			if ( owningComponent != null )
			{
				serializedComponents.Add( owningComponent );
			}
		}

		foreach ( var component in serializedComponents )
		{
			component.SerializeToProperty();
		}
	}

	void OnPaintEnded()
	{
		Log.Info( "Paint End" );

		// Close undo scope to finalize the undoable action
		_undoScope?.Dispose();
		_undoScope = null;
	}

	/// <summary>
	/// Find the ClutterComponent that contains the specified layer
	/// </summary>
	private ClutterComponent GetComponentForLayer( ClutterLayer layer )
	{
		if ( layer == null ) return null;

		var allComponents = Scene.GetAllComponents<ClutterComponent>();
		foreach ( var component in allComponents )
		{
			if ( component.Layers.Contains( layer ) )
			{
				return component;
			}
		}
		return null;
	}

	/// <summary>
	/// Get all assets from a clutter layer as a simple list (for validation)
	/// </summary>
	private List<Asset> GetAssetsFromClutterLayer( ClutterLayer layer )
	{
		var assets = new List<Asset>();
		foreach ( var clutterObject in layer.Objects )
		{
			var asset = AssetSystem.FindByPath( clutterObject.Path );
			if ( asset != null )
			{
				assets.Add( asset );
			}
		}
		return assets;
	}

	/// <summary>
	/// Get a weighted random asset from a clutter layer based on object weights
	/// </summary>
	private Asset GetWeightedRandomAssetFromLayer( ClutterLayer layer )
	{
		if ( layer.Objects.Count == 0 )
			return null;

		// Calculate total weight
		float totalWeight = 0f;
		foreach ( var clutterObject in layer.Objects )
		{
			totalWeight += clutterObject.Weight;
		}

		if ( totalWeight <= 0f )
		{
			// Fallback to equal weights if all weights are 0
			var randomIndex = Game.Random.Int( 0, layer.Objects.Count - 1 );
			var randomClutterObject = layer.Objects[randomIndex];
			return AssetSystem.FindByPath( randomClutterObject.Path );
		}

		// Generate random number between 0 and total weight
		float randomValue = Game.Random.Float( 0f, totalWeight );

		// Find the object corresponding to this weight
		float currentWeight = 0f;
		foreach ( var clutterObject in layer.Objects )
		{
			currentWeight += clutterObject.Weight;
			if ( randomValue <= currentWeight )
			{
				return AssetSystem.FindByPath( clutterObject.Path );
			}
		}

		// Fallback (should never reach here)
		return AssetSystem.FindByPath( layer.Objects.Last().Path );
	}

	/// <summary>
	/// Get or create a parent GameObject for the specified layer under the clutter component
	/// </summary>
	private GameObject GetOrCreateLayerParent( ClutterComponent clutterComponent, ClutterLayer layer )
	{
		// Look for existing layer parent by name
		var clutterGameObject = clutterComponent.GameObject;
		foreach ( var child in clutterGameObject.Children )
		{
			if ( child.Name == layer.Name )
			{
				return child;
			}
		}

		// Create new layer parent GameObject
		var layerParent = new GameObject();
		layerParent.Name = layer.Name;
		layerParent.Parent = clutterGameObject;
		layerParent.Tags.Add( "clutter_layer" );

		return layerParent;
	}

	/// <summary>
	/// Get cached bounds for an asset, calculating and caching if not already cached
	/// </summary>
	private BBox GetCachedAssetBounds( Asset asset )
	{
		if ( _assetBoundsCache.TryGetValue( asset.Path, out var cachedBounds ) )
		{
			return cachedBounds;
		}

		// Calculate bounds based on asset type
		BBox bounds = BBox.FromPositionAndSize( Vector3.Zero, 50f ); // Default size

		if ( asset.Path.EndsWith( ".prefab" ) )
		{
			if ( asset.TryLoadResource<PrefabFile>( out var prefab ) )
			{
				var prefabScene = SceneUtility.GetPrefabScene( prefab );
				if ( prefabScene?.Children?.FirstOrDefault() is GameObject prefabRoot )
				{
					bounds = prefabRoot.GetBounds();
				}
			}
		}
		else if ( asset.Path.EndsWith( ".vmdl" ) )
		{
			if ( asset.TryLoadResource<Model>( out var model ) )
			{
				bounds = model.Bounds;
			}
		}

		// Cache the calculated bounds
		_assetBoundsCache[asset.Path] = bounds;
		return bounds;
	}

	/// <summary>
	/// Get bounds for a GameObject (similar to the logic used for new objects)
	/// </summary>
	private BBox GetGameObjectBounds( GameObject gameObject )
	{
		// Try to get bounds from ModelRenderer first
		var modelRenderer = gameObject.Components.Get<ModelRenderer>();
		if ( modelRenderer != null && modelRenderer.Model != null )
		{
			return modelRenderer.Model.Bounds;
		}

		// Try to get bounds from prefab
		if ( gameObject.IsPrefabInstance )
		{
			try
			{
				var bounds = gameObject.GetBounds();
				if ( bounds.Size.Length > 0 )
					return bounds;
			}
			catch
			{
				// GetBounds can be expensive and sometimes fail, fallback to default
			}
		}

		// Default size if we can't determine bounds
		return BBox.FromPositionAndSize( Vector3.Zero, 50f );
	}

}

/// <summary>
/// Overlay window for scatter tool controls
/// </summary>
public class ScatterToolOverlay : WidgetWindow
{
	private readonly ScatterTool Tool;

	public ScatterToolOverlay( ScatterTool tool ) : base( tool.SceneOverlay, "Scatter Tool" )
	{
		Tool = tool;

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 4;
		MinimumWidth = 300.0f;
		MaximumWidth = 400.0f;

		var so = EditorTypeLibrary.GetSerializedObject( tool._settings );

		// Brush controls
		var brushLabel = new Label( "Brush Settings" );
		brushLabel.SetStyles( "font-weight: bold;" );
		Layout.Add( brushLabel );

		var brushCs = new ControlSheet();
		brushCs.AddRow( so.GetProperty( nameof( InspectorSettings.BrushSize ) ) );
		brushCs.AddRow( so.GetProperty( nameof( InspectorSettings.BrushOpacity ) ) );
		Layout.Add( brushCs );

		Layout.AddSeparator();

		// Scatterer type
		var scattererLabel = new Label( "Scatterer Algorithm" );
		scattererLabel.SetStyles( "font-weight: bold;" );
		Layout.Add( scattererLabel );

		// Create horizontal layout for scatterer dropdown and settings button
		var scattererRow = Layout.AddRow();
		scattererRow.Spacing = 4;

		var scattererCs = new ControlSheet();
		scattererCs.AddRow( so.GetProperty( nameof( InspectorSettings.ScattererName ) ) );
		scattererRow.Add( scattererCs, 1 );

		// Add scatterer settings button
		var scattererSettingsButton = new Button( "", "settings" )
		{
			StatusTip = "Configure scatterer settings",
			Clicked = () => OpenScattererSettings(),
			FixedWidth = 28,
			FixedHeight = 28
		};
		scattererRow.Add( scattererSettingsButton );

		Layout.AddSeparator();

		// Clutter layer
		var layerLabel = new Label( "Clutter Layers" );
		layerLabel.SetStyles( "font-weight: bold;" );
		Layout.Add( layerLabel );

		var clutterLayerCs = new ControlSheet();
		clutterLayerCs.AddRow( so.GetProperty( nameof( InspectorSettings.SelectedClutterLayers ) ) );
		Layout.Add( clutterLayerCs );

		Layout.AddSeparator();

		// Info labels
		var infoLabel = new Label( "Hold Ctrl to toggle mode" );
		infoLabel.SetStyles( "font-size: 11px; color: #888;" );

		Layout.Add( infoLabel );
	}

	private void OpenScattererSettings()
	{
		var scatterer = Tool._settings.Scatterer;
		if ( scatterer == null ) return;

		// Save settings when inspector is opened (in case user modifies and doesn't close tool properly)
		// We'll hook into the inspector's OnDestroy or update cycle
		Tool._settings.SaveScattererSettings();

		// Set the scatterer as the inspector object
		EditorUtility.InspectorObject = scatterer;
	}
}
