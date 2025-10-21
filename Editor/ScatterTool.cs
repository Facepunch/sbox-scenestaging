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

	// Scatter brush resource
	[Property, Group( "Source" )]
	public ScatterBrush Brush { get; set; }

	// Clutter system integration (only shown if no brush is selected)
	[Editor( "clutter_layer_multi" ), Group( "Source" ), ShowIf( nameof( ShouldShowLayerSelection ), true )]
	public List<ClutterLayer> SelectedClutterLayers { get; set; } = [];

	/// <summary>
	/// Only show layer selection if no brush is assigned
	/// </summary>
	private bool ShouldShowLayerSelection => Brush == null;

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

			var scattererType = Game.TypeLibrary.GetTypes<ScattererBase>()
				.FirstOrDefault( t => !t.IsAbstract && !t.IsInterface && t.Name == value );

			if ( scattererType != null )
			{
				_scatterer = scattererType.Create<ScattererBase>();

				// Try to load saved settings for this scatterer type
				LoadScattererSettings();

				EditorCookie.Set( CookiePrefix + "ScattererType", value );
			}
		}
	}

	private ScattererBase _scatterer;

	[Property]
	public ScattererBase Scatterer
	{
		get => _scatterer;
		set
		{
			_scatterer = value;
			SaveScattererSettings();
		}
	}

	public InspectorSettings()
	{
		var savedScattererType = EditorCookie.Get<string>( CookiePrefix + "ScattererType", null );
		if ( !string.IsNullOrEmpty( savedScattererType ) )
		{
			var scattererType = Game.TypeLibrary.GetTypes<ScattererBase>()
				.FirstOrDefault( t => !t.IsAbstract && !t.IsInterface && t.Name == savedScattererType );

			_scatterer = scattererType?.Create<ScattererBase>() ?? new DefaultScatterer();
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

		_previewObject?.Delete();
		_previewObject = null;

		_globalResourceCache.Clear();
		_assetBoundsCache.Clear();

		// Save scatterer settings
		_settings.SaveScattererSettings();
	}

	public override void OnUpdate()
	{
		var ctlrHeld = Gizmo.IsCtrlPressed;
		if ( Gizmo.IsCtrlPressed && !_settings.EraseMode )
		{
			_settings.EraseMode = true;
		}

		DrawBrushPreview();

		// Hitbox to capture all mouse events and prevent entity selection
		Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 999999 ) );

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

		// Set brush color, red for erase, blue for scatter
		var color = _settings.EraseMode ? Color.FromBytes( 250, 150, 150 ) : Color.FromBytes( 150, 150, 250 );
		color.a = _settings.BrushOpacity;

		var brush = TerrainEditorTool.Brush;
		var previewPosition = tr.HitPosition + tr.Normal * 1f;
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
		var tr = Trace.UseRenderMeshes( true ).WithTag( "solid" ).WithoutTags( "scattered_object" ).Run();
		if ( !tr.Hit )
			return;

		var brushRadius = _settings.BrushSize * 50f;
		var brushCenter = tr.HitPosition;

		if ( _settings.EraseMode )
		{
			List<ClutterLayer> layersToErase = [];

			// Erase only from those layers
			if ( _settings.SelectedClutterLayers.Count > 0 )
			{
				layersToErase.AddRange( _settings.SelectedClutterLayers );
			}
			else
			{
				var clutterSystem = Scene.GetSystem<ClutterSystem>();
				if ( clutterSystem != null )
				{
					layersToErase.AddRange( clutterSystem.GetAllLayers() );
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

		// Get layers from brush or selection
		List<ClutterLayer> layersToUse;

		if ( _settings.Brush != null && _settings.Brush.Layers?.Count > 0 )
		{
			// Use layers from the brush
			layersToUse = _settings.Brush.Layers;
		}
		else if ( _settings.SelectedClutterLayers.Count > 0 )
		{
			// Use manually selected layers
			layersToUse = _settings.SelectedClutterLayers;
		}
		else
		{
			Log.Warning( "Please select a Scatter Brush or at least one Clutter Layer to scatter objects" );
			return;
		}

		var scatterer = new ClutterScatterer( Scene )
			.WithBrush( brushCenter, brushRadius, _settings.BrushOpacity )
			.WithLayers( layersToUse );

		if ( _settings.Scatterer != null )
		{
			scatterer.WithScatterer( _settings.Scatterer );
		}

		scatterer.Run();

		// Serialization now happens automatically through ClutterSystem metadata
	}

	void OnPaintEnded()
	{
		_undoScope?.Dispose();
		_undoScope = null;
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
		var scattererLabel = new Label( "Scatterer Type" );
		scattererLabel.SetStyles( "font-weight: bold;" );
		Layout.Add( scattererLabel );

		// Create horizontal row for dropdown and button
		var scattererRow = Layout.AddRow();
		scattererRow.Spacing = 0;

		// Add the dropdown control
		var scattererCs = new ControlSheet();
		scattererCs.AddRow( so.GetProperty( nameof( InspectorSettings.ScattererName ) ) );
		scattererRow.Add( scattererCs, 1 );

		// Add settings button
		var settingsButton = new IconButton( "settings" )
		{
			StatusTip = "Configure scatterer settings",
			OnClick = () => OpenScattererSettings(),
			FixedWidth = 24,
			FixedHeight = 24
		};
		scattererRow.Add( settingsButton );

		Layout.AddSeparator();

		// Source section (Brush or Layers)
		var sourceLabel = new Label( "Layer Source" );
		sourceLabel.SetStyles( "font-weight: bold;" );
		Layout.Add( sourceLabel );

		var sourceCs = new ControlSheet();
		sourceCs.AddRow( so.GetProperty( nameof( InspectorSettings.Brush ) ) );
		sourceCs.AddRow( so.GetProperty( nameof( InspectorSettings.SelectedClutterLayers ) ) );
		Layout.Add( sourceCs );

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

		Tool._settings.SaveScattererSettings();

		EditorUtility.InspectorObject = scatterer;
	}
}
