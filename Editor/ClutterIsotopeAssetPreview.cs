using Editor;
using Editor.Assets;
using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Editor;

/// <summary>
/// Custom 3D asset preview for ClutterIsotope resources.
/// Uses the actual clutter system to generate a realistic preview.
/// </summary>
[AssetPreview( "isotope" )]
public class ClutterIsotopeAssetPreview : AssetPreview
{
	private ClutterIsotope _currentIsotope;
	private GameObject _clutterObject;
	private GameObject _groundPlane;
	private int _lastIsotopeHash;
	private float _previewTileSize = 512f;

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
			// Create ground plane for collision
			CreateGroundPlane();

			// Create clutter object and scatter immediately
			CreateClutterObject();
			
			// Store initial state
			UpdateIsotopeState();
			
			// Calculate bounds immediately after generation
			CalculateSceneBounds();
		}
	}

	public override void UpdateScene( float cycle, float timeStep )
	{
		// Check if isotope has changed and regenerate if needed
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

	private async void RegeneratePreview()
	{
		using ( Scene.Push() )
		{
			// Destroy and recreate the clutter object
			if ( _clutterObject.IsValid() )
			{
				_clutterObject.Destroy();
			}

			// Recreate and manually scatter
			CreateClutterObject();
			
			// Update state
			UpdateIsotopeState();
			
			// Small delay for scene to update
			await Task.Delay( 50 );
			
			CalculateSceneBounds();
		}
	}

	private void CreateClutterObject()
	{
		// Create clutter object for preview
		_clutterObject = new GameObject( true, "Clutter Preview" );
		PrimaryObject = _clutterObject;
		
		// Manually generate preview scatter instead of relying on system
		if ( _currentIsotope?.Scatterer != null )
		{
			var bounds = new BBox( 
				new Vector3( -_previewTileSize / 2, -_previewTileSize / 2, -100 ), 
				new Vector3( _previewTileSize / 2, _previewTileSize / 2, 100 ) 
			);
			
			var job = ClutterGenerationJob.Volume(
				bounds: bounds,
				cellSize: _previewTileSize,
				randomSeed: 42, // Fixed seed for consistent preview
				isotope: _currentIsotope,
				parentObject: _clutterObject,
				onComplete: null
			);
			
			// Execute immediately, synchronously
			job.Execute();
		}
	}

	private void CreateGroundPlane()
	{
		// Create a simple ground plane for collision
		_groundPlane = new GameObject( true, "Ground" );
		
		// Add collider for trace testing
		var collider = _groundPlane.Components.Create<BoxCollider>();
		collider.Scale = new Vector3( 5000, 5000, 10 );
		collider.Center = new Vector3( 0, 0, -5 );
		_groundPlane.WorldPosition = new Vector3( 0, 0, 0 );
	}

	private void CalculateSceneBounds()
	{
		if ( !_clutterObject.IsValid() )
			return;

		// Get all spawned clutter objects
		var allObjects = _clutterObject.Children.ToList();
		
		if ( allObjects.Count == 0 )
		{
			// Default bounds if nothing spawned yet
			SceneCenter = new Vector3( 0, 0, 50 );
			SceneSize = new Vector3( 200, 200, 100 );
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
		
		_clutterObject?.Destroy();
		_clutterObject = null;
		
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
		randomizeBtn.Clicked += async () =>
		{
			using ( Scene.Push() )
			{
				if ( _clutterObject.IsValid() )
				{
					// Destroy current clutter
					_clutterObject.Destroy();
					
					// Create new with random seed
					_clutterObject = new GameObject( true, "Clutter Preview" );
					PrimaryObject = _clutterObject;
					
					if ( _currentIsotope?.Scatterer != null )
					{
						var bounds = new BBox( 
							new Vector3( -_previewTileSize / 2, -_previewTileSize / 2, -100 ), 
							new Vector3( _previewTileSize / 2, _previewTileSize / 2, 100 ) 
						);
						
						var job = ClutterGenerationJob.Volume(
							bounds: bounds,
							cellSize: _previewTileSize,
							randomSeed: DateTime.Now.Ticks.GetHashCode(), // Random seed
							isotope: _currentIsotope,
							parentObject: _clutterObject,
							onComplete: null
						);
						
						job.Execute();
					}
					
					await Task.Delay( 50 );
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
