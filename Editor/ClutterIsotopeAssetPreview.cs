using Editor;
using Editor.Assets;
using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Editor;

/// <summary>
/// Custom 3D asset preview for ClutterIsotope resources.
/// Shows a preview of what a single tile would look like with the isotope's scatterer settings.
/// </summary>
[AssetPreview( "isotope" )]
public class ClutterIsotopeAssetPreview : AssetPreview
{
	private ClutterIsotope _currentIsotope;
	private GameObject _previewContainer;
	private GameObject _groundPlane;
	private int _lastIsotopeHash;
	private float _previewTileSize = 512f; // Larger default for better preview

	public ClutterIsotopeAssetPreview( Asset asset ) : base( asset )
	{
	}

	/// <summary>
	/// Slow down the rotation speed for a better view of the scattered objects
	/// </summary>
	public override float PreviewWidgetCycleSpeed => 0.15f;

	public override async Task InitializeAsset()
	{
		await base.InitializeAsset();

		_currentIsotope = Asset.LoadResource<ClutterIsotope>();
		
		if ( _currentIsotope == null )
			return;

		using ( Scene.Push() )
		{
			// Create ground plane first
			CreateGroundPlane();

			// Create container for clutter objects
			_previewContainer = new GameObject( true, "Clutter Preview" );
			PrimaryObject = _previewContainer;

			// Generate clutter
			GeneratePreviewTile();
			
			// Calculate bounds after generation
			CalculateSceneBounds();
			
			// Store initial state
			UpdateIsotopeState();
		}
	}

	public override void UpdateScene( float cycle, float timeStep )
	{
		// Check if isotope has changed and regenerate if needed (immediate check)
		if ( CheckForChanges() )
		{
			RegeneratePreview();
		}

		base.UpdateScene( cycle, timeStep );
	}

	private bool CheckForChanges()
	{
		if ( _currentIsotope == null )
			return false;

		var currentHash = GetIsotopeHash();
		return currentHash != _lastIsotopeHash;
	}

	private void UpdateIsotopeState()
	{
		if ( _currentIsotope == null )
			return;

		_lastIsotopeHash = GetIsotopeHash();
	}

	private int GetIsotopeHash()
	{
		if ( _currentIsotope == null )
			return 0;

		// Generate hash from all relevant isotope properties
		var hash = new HashCode();
		
		hash.Add( _currentIsotope.Entries.Count );
		
		foreach ( var entry in _currentIsotope.Entries )
		{
			if ( entry != null )
			{
				hash.Add( entry.Weight );
				hash.Add( entry.Model?.GetHashCode() ?? 0 );
				hash.Add( entry.Prefab?.GetHashCode() ?? 0 );
			}
		}
		
		// Add scatterer hash
		hash.Add( _currentIsotope.Scatterer?.GetHashCode() ?? 0 );
		
		return hash.ToHashCode();
	}

	private void RegeneratePreview()
	{
		using ( Scene.Push() )
		{
			// Clear old clutter objects
			if ( _previewContainer.IsValid() )
			{
				foreach ( var child in _previewContainer.Children.ToArray() )
				{
					child.Destroy();
				}
			}

			// Regenerate
			GeneratePreviewTile();
			CalculateSceneBounds();
			
			// Update state
			UpdateIsotopeState();
		}
	}

	private void CreateGroundPlane()
	{
		// Create a simple ground plane for collision only
		_groundPlane = new GameObject( true, "Ground" );
		
		// Add collider for trace testing - no visual model needed
		var collider = _groundPlane.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( 5000, 5000, 10 );
		collider.Center = new Vector3( 0, 0, -5 );
		_groundPlane.WorldPosition = new Vector3( 0, 0, 0 );
	}

	private void GeneratePreviewTile()
	{
		if ( _currentIsotope == null || _currentIsotope.Scatterer == null )
			return;

		if ( _currentIsotope.ValidEntryCount == 0 )
			return;

		// Create bounds centered at origin, extending upward
		// This ensures objects can be scattered above ground and traced down
		var bounds = new BBox(
			new Vector3( -_previewTileSize / 2, -_previewTileSize / 2, -10 ),
			new Vector3( _previewTileSize / 2, _previewTileSize / 2, 200 )
		);

		// Use a fixed seed for consistent preview
		var random = new Random( 42 );

		// Scatter using the isotope's scatterer
		try
		{
			_currentIsotope.Scatterer.ScatterInVolume( bounds, _currentIsotope, _previewContainer, random );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to generate isotope preview: {e.Message}" );
		}
	}

	private void CalculateSceneBounds()
	{
		if ( !_previewContainer.IsValid() )
			return;

		var allObjects = _previewContainer.Children.ToList();
		
		if ( allObjects.Count == 0 )
		{
			// Default bounds if nothing spawned
			SceneCenter = new Vector3( 0, 0, 50 );
			SceneSize = new Vector3( 100, 100, 100 );
			return;
		}

		// Find bounding box of all objects
		var bbox = new BBox();
		bool hasValidBounds = false;

		foreach ( var obj in allObjects )
		{
			if ( obj.Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model != null )
			{
				var objBounds = renderer.Model.Bounds.Transform( obj.WorldTransform );
				bbox = bbox.AddBBox( objBounds );
				hasValidBounds = true;
			}
			else
			{
				bbox = bbox.AddPoint( obj.WorldPosition );
			}
		}

		if ( !hasValidBounds || bbox.Size.Length < 1 )
		{
			// Fallback to a reasonable size
			bbox = BBox.FromPositionAndSize( Vector3.Zero, 200 );
		}

		// Include ground in bounds
		bbox = bbox.AddPoint( new Vector3( 0, 0, 0 ) );

		SceneCenter = bbox.Center;
		SceneSize = bbox.Size;
	}

	public override void Dispose()
	{
		_groundPlane?.Destroy();
		_groundPlane = null;
		
		_previewContainer?.Destroy();
		_previewContainer = null;
		
		base.Dispose();
	}

	/// <summary>
	/// Create a toolbar with interactive buttons shown when hovering
	/// </summary>
	public override Widget CreateToolbar()
	{
		var toolbar = new Widget();
		toolbar.Layout = Layout.Row();
		toolbar.Layout.Spacing = 8;
		toolbar.Layout.Margin = 8;

		// Randomize button - generate with new random seed
		var randomizeBtn = new Button( "Randomize", "casino" );
		randomizeBtn.Clicked += () =>
		{
			using ( Scene.Push() )
			{
				if ( _previewContainer.IsValid() )
				{
					foreach ( var child in _previewContainer.Children.ToArray() )
					{
						child.Destroy();
					}

					var bounds = new BBox(
						new Vector3( -_previewTileSize / 2, -_previewTileSize / 2, -10 ),
						new Vector3( _previewTileSize / 2, _previewTileSize / 2, 200 )
					);

					var random = new Random( DateTime.Now.Ticks.GetHashCode() );
					_currentIsotope.Scatterer.ScatterInVolume( bounds, _currentIsotope, _previewContainer, random );
					
					CalculateSceneBounds();
				}
			}
		};
		randomizeBtn.ToolTip = "Generate with a random seed to see variations";
		toolbar.Layout.Add( randomizeBtn );

		return toolbar;
	}

	/// <summary>
	/// Create widgets that are always visible in the preview
	/// </summary>
	public override Widget CreateWidget( Widget parent )
	{
		// Don't add any overlays - keep the 3D preview clean
		return null;
	}
}
