using Editor.AssetPickers;
using Sandbox;
using SceneStaging;
using System;

namespace Editor;

[AssetPicker( typeof( OctahedralImposterAsset ) )]
internal class ImposterPicker : SimplePicker
{
	public ImposterPicker( Widget parent, AssetType assetType, PickerOptions options ) : base( parent, assetType, options )
	{
	}
}

/// <summary>
/// Preview widget with orbit camera controls.
/// </summary>
internal class ImposterPreviewWidget : SceneRenderingWidget
{
	private Vector2 _lastCursorPos;
	private Vector2 _cursorDelta;
	private Vector2 _angles = new Vector2( 45, 30 ); // yaw, pitch
	private Vector3 _origin = Vector3.Zero;
	private float _distance = 200f;
	private BBox _objectBounds = new BBox( -16, 16 ); // Bounds of the object being previewed
	private bool _orbitControl;
	private bool _zoomControl;
	private bool _showCameraGizmos = true;

	public Vector3 Origin
	{
		get => _origin;
		set => _origin = value;
	}

	public bool ShowCameraGizmos
	{
		get => _showCameraGizmos;
		set => _showCameraGizmos = value;
	}

	public ImposterPreviewWidget( Widget parent ) : base( parent )
	{
	}

	protected override void PreFrame()
	{
		base.PreFrame();

		if ( _orbitControl )
		{
			var cursorPos = Application.CursorPosition;
			_cursorDelta = cursorPos - _lastCursorPos;

			if ( _cursorDelta.Length > 0.0f )
			{
				_angles.x += _cursorDelta.x * 0.2f;
				_angles.y += _cursorDelta.y * 0.2f;
				_angles.y = _angles.y.Clamp( -90, 90 );
				_angles.x = _angles.x.NormalizeDegrees();
			}

			Application.CursorPosition = _lastCursorPos;
			Cursor = CursorShape.Blank;
		}
		else if ( _zoomControl )
		{
			var cursorPos = Application.CursorPosition;
			_cursorDelta = cursorPos - _lastCursorPos;

			if ( Math.Abs( _cursorDelta.y ) > 0.0f )
			{
				Zoom( _cursorDelta.y );
			}

			Application.CursorPosition = _lastCursorPos;
			Cursor = CursorShape.Blank;
		}
		else
		{
			Cursor = CursorShape.None;
		}

		// Update camera
		if ( Scene?.Camera != null )
		{
			Scene.Camera.WorldRotation = new Angles( _angles.y, -_angles.x, 0 );
			Scene.Camera.WorldPosition = _origin + Scene.Camera.WorldRotation.Backward * _distance;
		}

		// Draw camera gizmos for the 8 octahedral positions
		if ( _showCameraGizmos )
		{
			DrawCameraGizmos();
		}

		Scene?.EditorTick( RealTime.Now, RealTime.Delta );
	}

	private void DrawCameraGizmos()
	{
		if ( Scene?.Camera == null ) return;

		// Calculate camera distance based on object bounds
		var boundsSize = _objectBounds.Size.Length;
		var cameraDistance = Math.Max( 16f, boundsSize * 0.5f ) * 2f;

		// 8 octahedral directions (4 cardinal + 4 diagonal, all at same elevation)
		var cameraPositions = new[]
		{
			Rotation.From( 30f, 0f, 0f ),    // Front
			Rotation.From( 30f, 45f, 0f ),   // Front-Right
			Rotation.From( 30f, 90f, 0f ),   // Right
			Rotation.From( 30f, 135f, 0f ),  // Back-Right
			Rotation.From( 30f, 180f, 0f ),  // Back
			Rotation.From( 30f, 225f, 0f ),  // Back-Left
			Rotation.From( 30f, 270f, 0f ),  // Left
			Rotation.From( 30f, 315f, 0f )   // Front-Left
		};

		foreach ( var rotation in cameraPositions )
		{
			var cameraPos = _origin - rotation.Forward * cameraDistance;

			// Draw camera icon
			Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.7f );
			Gizmo.Draw.LineThickness = 2f;

			// Draw camera pyramid/frustum
			var forward = rotation.Forward;
			var right = rotation.Right;
			var up = rotation.Up;
			var size = cameraDistance * 0.1f;

			var nearCenter = cameraPos + forward * size * 0.5f;
			var farTL = nearCenter + up * size * 0.3f - right * size * 0.3f;
			var farTR = nearCenter + up * size * 0.3f + right * size * 0.3f;
			var farBL = nearCenter - up * size * 0.3f - right * size * 0.3f;
			var farBR = nearCenter - up * size * 0.3f + right * size * 0.3f;

			// Draw pyramid from camera position to frustum
			Gizmo.Draw.Line( cameraPos, farTL );
			Gizmo.Draw.Line( cameraPos, farTR );
			Gizmo.Draw.Line( cameraPos, farBL );
			Gizmo.Draw.Line( cameraPos, farBR );

			// Draw frustum rectangle
			Gizmo.Draw.Line( farTL, farTR );
			Gizmo.Draw.Line( farTR, farBR );
			Gizmo.Draw.Line( farBR, farBL );
			Gizmo.Draw.Line( farBL, farTL );
		}
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		if ( e.LeftMouseButton )
		{
			_orbitControl = true;
			_lastCursorPos = Application.CursorPosition;
		}
		else if ( e.RightMouseButton )
		{
			_zoomControl = true;
			_lastCursorPos = Application.CursorPosition;
		}
	}

	protected override void OnMouseReleased( MouseEvent e )
	{
		base.OnMouseReleased( e );

		if ( e.LeftMouseButton )
		{
			_orbitControl = false;
		}
		else if ( e.RightMouseButton )
		{
			_zoomControl = false;
		}
	}

	protected override void OnWheel( WheelEvent e )
	{
		base.OnWheel( e );
		Zoom( e.Delta * -5f );
	}

	private void Zoom( float delta )
	{
		_distance += delta;
		_distance = _distance.Clamp( 10, 10000 );
	}

	public void SetDistance( float distance )
	{
		_distance = distance;
	}

	public void SetObjectBounds( BBox bounds )
	{
		_objectBounds = bounds;
	}
}


internal static class ImposterMenu
{
	[Event( "asset.contextmenu", Priority = 50 )]
	public static void OnPrefabFileAssetContext( AssetContextMenu e )
	{
		Log.Info( e.SelectedList.First().AssetType );
		if ( !e.SelectedList.All( x => x.AssetType == AssetType.FromExtension(".prefab") ) )
			return;

		e.Menu.AddOption( $"Create Imposter", "audio_file", action: () => CreateImposterUsingPrefabs( e.SelectedList ) );
	}

	public static void CreateImposterUsingPrefabs( IEnumerable<AssetEntry> entries )
	{

	}
}

/// <summary>
/// Editor window for managing and generating octahedral imposters.
/// </summary>
[EditorApp( "Octahedral Imposter Baker", "view_in_ar", "Generate octahedral imposters from prefabs" )]
public class ImposterEditorWindow : BaseWindow
{
	private Asset selectedPrefab;
	private int resolutionPerView = 512;
	private bool includeNormals = false;
	private bool includeDepth = false;

	private Button bakeButton;
	private Label statusLabel;

	private Scene previewScene;
	private ImposterPreviewWidget previewWidget;
	private CameraComponent previewCamera;
	private GameObject prefabInstance;
	private GameObject lightObject;

	public ImposterEditorWindow() : base()
	{
		WindowTitle = "Octahedral Imposter Baker";
		SetWindowIcon( "view_in_ar" );

		Size = new Vector2( 800, 600 );
		MinimumSize = new Vector2( 600, 500 );

		CreatePreviewScene();
		InitializeUI();
	}

	public override void OnDestroyed()
	{
		base.OnDestroyed();
	}

	private void CreatePreviewScene()
	{
		previewScene = Scene.CreateEditorScene();
		using ( previewScene.Push() )
		{
			// Create camera
			var cameraObject = new GameObject( true, "camera" );
			previewCamera = cameraObject.Components.Create<CameraComponent>();
			previewCamera.WorldPosition = new Vector3( -100, -100, 50 );
			previewCamera.WorldRotation = Rotation.LookAt( Vector3.Zero - previewCamera.WorldPosition );
			previewCamera.FieldOfView = 60f;
			previewCamera.BackgroundColor = new Color( 0.1f, 0.1f, 0.1f );

			// Create directional light
			lightObject = new GameObject( true, "Directional Light" );
			lightObject.WorldRotation = Rotation.From( 80f, 30f, 0f );
			var light = lightObject.Components.Create<DirectionalLight>();
			light.LightColor = Color.White * 2.5f + Color.Cyan * 0.05f;
			light.SkyColor = Color.White;
		}
	}

	private void InitializeUI()
	{
		Layout = Layout.Row();
		Layout.Margin = 8;
		Layout.Spacing = 8;

		// Left panel - settings (wrapped in a widget to control width)
		var leftContainer = Layout.Add( new Widget( this ) );
		leftContainer.MaximumWidth = 350;
		leftContainer.Layout = Layout.Column();

		var leftPanel = leftContainer.Layout;
		leftPanel.Spacing = 12;
		leftPanel.Margin = 8;

		// Header
		leftPanel.Add( new Label.Title( "Octahedral Imposter Baker" ) );
		leftPanel.Add( new Label( "Generate octahedral imposters from prefabs for efficient LOD rendering." ) { WordWrap = true } );

		leftPanel.AddSeparator();

		// Prefab Selection
		var prefabRow = leftPanel.AddRow();
		prefabRow.Spacing = 8;
		prefabRow.Add( new Label( "Prefab:" ) { MinimumWidth = 100 } );

		var prefabAssetType = AssetType.Find( "prefab", false );
		var prefabPicker = new ImposterPicker( this, prefabAssetType, new AssetPicker.PickerOptions { } );
		prefabPicker.MinimumWidth = 200;
		prefabPicker.OnAssetPicked = ( assets ) =>
		{
			selectedPrefab = assets.FirstOrDefault();
			UpdateBakeButton();
			UpdatePreview();
		};
		prefabRow.Add( prefabPicker, 1 );

		// Resolution Setting
		var resRow = leftPanel.AddRow();
		resRow.Spacing = 8;
		resRow.Add( new Label( "Resolution:" ) { MinimumWidth = 100 } );

		var resCombo = new ComboBox( this );
		resCombo.AddItem( "256x256", null, () => { resolutionPerView = 256; } );
		resCombo.AddItem( "512x512", null, () => { resolutionPerView = 512; } );
		resCombo.AddItem( "1024x1024", null, () => { resolutionPerView = 1024; } );
		resCombo.AddItem( "2048x2048", null, () => { resolutionPerView = 2048; } );
		resCombo.CurrentIndex = 1;
		resCombo.MinimumWidth = 150;
		resRow.Add( resCombo );
		resRow.AddStretchCell();

		leftPanel.AddSeparator();

		// Options
		leftPanel.Add( new Label.Subtitle( "Options" ) );

		var normalsCheckbox = leftPanel.Add( new Checkbox( "Include Normal Maps" ) );
		normalsCheckbox.Value = includeNormals;
		normalsCheckbox.StateChanged = ( value ) => includeNormals = value == CheckState.On;

		var depthCheckbox = leftPanel.Add( new Checkbox( "Include Depth Maps" ) );
		depthCheckbox.Value = includeDepth;
		depthCheckbox.StateChanged = ( value ) => includeDepth = value == CheckState.On;

		leftPanel.AddStretchCell();

		// Status Label
		statusLabel = leftPanel.Add( new Label( "Select a prefab to begin." ) );
		statusLabel.MinimumHeight = 20;

		leftPanel.AddSeparator();

		// Bake Button
		var buttonRow = leftPanel.AddRow();
		buttonRow.AddStretchCell();
		bakeButton = buttonRow.Add( new Button.Primary( "Bake Imposter" ) );
		bakeButton.Clicked = OnBakeClicked;
		bakeButton.Enabled = false;
		bakeButton.MinimumWidth = 150;

		// Right panel - preview
		var rightPanel = Layout.AddColumn();
		rightPanel.Spacing = 8;
		rightPanel.Margin = 8;

		rightPanel.Add( new Label.Subtitle( "Preview" ) );

		previewWidget = new ImposterPreviewWidget( this );
		previewWidget.Scene = previewScene;
		previewWidget.MinimumSize = new Vector2( 300, 300 );
		rightPanel.Add( previewWidget, 1 );
	}

	private void UpdateBakeButton()
	{
		bakeButton.Enabled = selectedPrefab != null;

		if ( selectedPrefab != null )
		{
			statusLabel.Text = $"Ready to bake: {selectedPrefab.Name}";
		}
		else
		{
			statusLabel.Text = "Select a prefab to begin.";
		}
	}

	private void UpdatePreview()
	{
		// Destroy old instance
		prefabInstance?.Destroy();
		prefabInstance = null;

		if ( selectedPrefab == null )
			return;

		// Load and instantiate prefab
		var prefabFile = selectedPrefab.LoadResource<PrefabFile>();
		if ( prefabFile == null )
			return;

		using ( previewScene.Push() )
		{
			var prefabScene = SceneUtility.GetPrefabScene( prefabFile );
			prefabInstance = prefabScene.Clone();

			// Position camera based on bounds
			var bounds = prefabInstance.GetBounds();
			var center = bounds.Center;
			var distance = Math.Max( 16f, bounds.Size.Length * 0.5f ) * 4f;

			// Set orbit camera origin, distance, and bounds for gizmos
			previewWidget.Origin = center;
			previewWidget.SetDistance( distance );
			previewWidget.SetObjectBounds( bounds );
		}
	}

	private void OnBakeClicked()
	{
		if ( selectedPrefab == null )
			return;

		bakeButton.Enabled = false;
		statusLabel.Text = "Baking imposter...";

		try
		{
			ImposterBaker.BakeImposter( selectedPrefab, resolutionPerView );
			statusLabel.Text = $"✓ Successfully baked imposter for {selectedPrefab.Name}";
		}
		catch ( Exception ex )
		{
			statusLabel.Text = $"Error: {ex.Message}";
			Log.Error( ex );
		}
		finally
		{
			bakeButton.Enabled = true;
		}
	}
}
