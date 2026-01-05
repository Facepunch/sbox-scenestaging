using Editor.Assets;
using Sandbox.Clutter;
using System;
using System.Threading.Tasks;

namespace Editor;

/// <summary>
/// Custom 3D asset preview for ClutterDefinition resources.
/// Uses the actual clutter system to generate a realistic preview.
/// </summary>
[AssetPreview( "clutter" )]
public class ClutterDefinitionAssetPreview( Asset asset ) : AssetPreview( asset )
{
	private ClutterDefinition _currentclutter;
	private GameObject _clutterObject;
	private GameObject _groundPlane;
	private int _lastclutterHash;
	private float _previewVolumeSize = 512f;

	/// <summary>
	/// Slow down the rotation speed for a better view of the scattered objects
	/// </summary>
	public override float PreviewWidgetCycleSpeed => 0.15f;

	public override async Task InitializeAsset()
	{
		await base.InitializeAsset();

		_currentclutter = Asset.LoadResource<ClutterDefinition>();
		
		if ( _currentclutter == null )
			return;

		using ( Scene.Push() )
		{
			// Create ground plane for collision
			CreateGroundPlane();

			// Create clutter object with component using regular pipeline
			CreateClutterObject();
			
			// Store initial state
			UpdateclutterState();
			
			// Small delay for system to generate
			await Task.Delay( 50 );
			
			// Calculate bounds after generation
			CalculateSceneBounds();
		}
	}

	public override void UpdateScene( float cycle, float timeStep )
	{
		// Check if clutter has changed and regenerate if needed
		if ( CheckForChanges() )
		{
			RegeneratePreview();
		}

		base.UpdateScene( cycle, timeStep );
	}

	private bool CheckForChanges()
	{
		if ( _currentclutter == null )
			return false;

		var currentHash = GetclutterHash();
		return currentHash != _lastclutterHash;
	}

	private void UpdateclutterState()
	{
		if ( _currentclutter == null )
			return;

		_lastclutterHash = GetclutterHash();
	}

	private int GetclutterHash()
	{
		if ( _currentclutter == null )
			return 0;

		// Generate hash from all relevant clutter properties
		var hash = new HashCode();
		
		hash.Add( _currentclutter.Entries.Count );
		
		foreach ( var entry in _currentclutter.Entries )
		{
			if ( entry != null )
			{
				hash.Add( entry.Weight );
				hash.Add( entry.Model?.GetHashCode() ?? 0 );
				hash.Add( entry.Prefab?.GetHashCode() ?? 0 );
			}
		}
		
		// Add scatterer hash
		hash.Add( _currentclutter.Scatterer?.GetHashCode() ?? 0 );
		
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

			// Recreate using regular pipeline
			CreateClutterObject();
			
			// Update state
			UpdateclutterState();
			
			// Small delay for system to generate
			await Task.Delay( 50 );
			
			CalculateSceneBounds();
		}
	}

	private void CreateClutterObject()
	{
		// Create clutter object for preview
		_clutterObject = new GameObject( true, "Clutter Preview" );
		PrimaryObject = _clutterObject;
		
		// Use ClutterComponent with Volume mode - this goes through the regular pipeline
		if ( _currentclutter != null )
		{
			var component = _clutterObject.Components.Create<ClutterComponent>();
			component.Clutter = _currentclutter;
			component.Infinite = false; // Use volume mode
			component.Bounds = new BBox(
				new Vector3( -_previewVolumeSize / 2, -_previewVolumeSize / 2, -100 ),
				new Vector3( _previewVolumeSize / 2, _previewVolumeSize / 2, 100 )
			);
			component.RandomSeed = 42; // Fixed seed for consistent preview
			
			// Generate immediately
			component.Generate();
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

		// Get all spawned clutter objects (prefabs only - models are in batches)
		var allObjects = _clutterObject.Children.ToList();
		
		// Start with volume bounds as base
		var bbox = new BBox(
			new Vector3( -_previewVolumeSize / 2, -_previewVolumeSize / 2, -100 ),
			new Vector3( _previewVolumeSize / 2, _previewVolumeSize / 2, 100 )
		);

		bool hasValidBounds = true;

		// Expand bounds to include spawned objects
		foreach ( var obj in allObjects )
		{
			if ( obj.Components.TryGet<ModelRenderer>( out var renderer ) && renderer.Model != null )
			{
				var objBounds = renderer.Model.Bounds.Transform( obj.WorldTransform );
				bbox = bbox.AddBBox( objBounds );
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
				if ( _clutterObject.IsValid() && _clutterObject.Components.TryGet<ClutterComponent>( out var component ) )
				{
					// Change seed and regenerate
					component.RandomSeed = Random.Shared.Next();
					component.Clear();
					component.Generate();
					
					await Task.Delay( 50 );
					CalculateSceneBounds();
				}
			}
		};
		randomizeBtn.ToolTip = "Generate with a random seed to see variations";
		toolbar.Layout.Add( randomizeBtn );

		return toolbar;
	}
}
