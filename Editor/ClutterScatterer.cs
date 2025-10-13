using System;
using System.Collections.Concurrent;
using Sandbox;
using Sandbox.Utility;

namespace Editor;

/// <summary>
/// This a builder pattern class to build a scatterer operation
/// </summary>
public sealed class ClutterScatterer( Scene scene )
{
	private Scene _scene = scene;
	private ScattererBase _scatterer = new DefaultScatterer();

	private bool _isErase;
	private bool _shouldClear;
	private bool _useVolume;
	private bool _useBrush;

	private ClutterVolumeComponent? _volume;
	private Vector3 _brushPosition;
	private float _brushRadius;
	private float _brushDensity;

	private List<ClutterLayer> _layers = [];
	private readonly ClutterResources _resources = new();

	/// <summary>
	/// Sets the procedural scatterer to use for generating points and scattering objects
	/// </summary>
	public ClutterScatterer WithScatterer( ScattererBase scatterer )
	{
		_scatterer = scatterer;
		return this;
	}

	/// <summary>
	/// If with a volume, will clear all existing clutter in the volume before scattering new instances
	/// </summary>
	public ClutterScatterer WithClear( bool clear = true )
	{
		_shouldClear = clear;
		return this;
	}

	/// <summary>
	/// Will erase in the area instead of scattering new instances
	/// </summary>
	public ClutterScatterer WithErase( bool erase = true )
	{
		_isErase = erase;
		return this;
	}

	/// <summary>
	/// Will scatter on the specified layers
	/// </summary>
	public ClutterScatterer WithLayers( IEnumerable<ClutterLayer> layers )
	{
		_layers = layers.ToList();
		return this;
	}

	/// <summary>
	/// Will scatter with the specified volume's settings and layers
	/// </summary>
	public ClutterScatterer WithVolume( ClutterVolumeComponent volume )
	{
		_volume = volume;
		_scene = volume.Scene;
		_useVolume = true;

		// Unsure if we like automatically grabbing these. It's "hidden" behavior..
		_scatterer ??= volume.Scatterer;

		_layers = [.. volume.GetActiveLayers()];

		return this;
	}

	/// <summary>
	/// Will execute the scatter operation with a brush of a specified position, radius and density
	/// </summary>
	public ClutterScatterer WithBrush( Vector3 position, float radius, float density )
	{
		_brushPosition = position;
		_brushRadius = radius;
		_brushDensity = density;
		_useBrush = true;
		return this;
	}

	/// <summary>
	/// Executes the built scatter operation, instances will be registered with the clutter system
	/// </summary>
	public void Run()
	{
		var system = _scene.GetSystem<ClutterSystem>();

		Vector3[] points = _useVolume
				? _scatterer.GeneratePoints( _volume!.GetScatterBounds(), _volume.Density ).ToArray()
				: GenerateBrushPoints();

		// For erase mode, we only need layers
		if ( _isErase )
		{
			EraseInstances( system, points );
			return;
		}

		// For scatter mode, we need layers
		if ( _layers.Count == 0 )
		{
			Log.Warning( "ClutterScatterer: Missing layers" );
			return;
		}

		if ( _shouldClear && _useVolume )
		{
			system.ClearVolume( _volume! );
		}

		if ( points.Length == 0 )
			return;

		// Preload
		PreloadResources( _layers );

		// Ensure layer parent GameObjects exist if using a volume
		if ( _useVolume )
		{
			foreach ( var layer in _layers )
			{
				_volume!.GetOrCreateLayerParent( layer );
			}
		}

		// Scatter
		var allInstances = ScatterPoints( points );

		// Instantiate any deferred prefabs on the main thread
		foreach ( var instance in allInstances )
		{
			instance.InstantiatePrefab();
		}

		// Register new instances with clutter system, layers and volume
		ProcessInstances( system, allInstances );
	}

	/// <summary>
	/// Basic position generator for a brush
	/// </summary>
	private Vector3[] GenerateBrushPoints()
	{
		var bounds = new BBox(
			_brushPosition - new Vector3( _brushRadius ),
			_brushPosition + new Vector3( _brushRadius ) );

		// Keep the points within the brush radius
		return _scatterer.GeneratePoints( bounds, _brushDensity )
			.Where( p => (p - _brushPosition).Length <= _brushRadius )
			.ToArray();
	}

	/// <summary>
	/// After generating points, we need to ray trace down to find the surface to scatter on.
	/// </summary>
	/// <param name="points"></param>
	/// <returns></returns>
	private List<ClutterInstance> ScatterPoints( Vector3[] points )
	{
		var instances = new ConcurrentBag<ClutterInstance>();

		Parallel.For( 0, points.Length, i =>
		{
			var point = points[i];
			var trace = _scene.Trace
				.Ray( point + Vector3.Up * 1000, point + Vector3.Down * 1000 )
				.UseRenderMeshes( true )
				.WithTag( "solid" )
				.WithoutTags( "scattered" )
				.Run();

			if ( !trace.Hit )
				return;

			ScatterContext ctx = new()
			{
				HitTest = trace,
				Resources = _resources,
				Density = _useVolume ? _volume!.Density : _brushDensity
			};

			foreach ( var layer in _layers )
			{
				if ( !layer.HasObjects )
					continue;

				var layerParent = _useVolume ? _volume!.GetOrCreateLayerParent( layer ) : null;
				var instance = _scatterer.Scatter( ctx with { Layer = layer, LayerParent = layerParent } );
				if ( instance is { } valid )
				{
					instances.Add( valid );
					break;
				}
			}
		} );

		return [.. instances];
	}

	/// <summary>
	/// After scattering new instances, we need to update the book keeping of the clutter associations. Volumes, Clutter and Cells(ClutterSystem).
	/// This will register all newly created instances with the clutter system and the layers and volume.
	/// </summary>
	private void ProcessInstances( ClutterSystem system, List<ClutterInstance> instances )
	{
		var instancesByLayer = _layers.ToDictionary( l => l, _ => new List<ClutterInstance>() );

		foreach ( var inst in instances )
		{
			foreach ( var layer in _layers )
			{
				bool matches = inst.ClutterType == ClutterInstance.Type.Model
					? layer.Objects.Any( o => o.Path == inst.model?.ResourcePath )
					: layer.Objects.Any( o => o.Path != null );

				if ( matches )
				{
					instancesByLayer[layer].Add( inst );
					break;
				}
			}
		}

		foreach ( var (layer, layerInstances) in instancesByLayer )
		{
			if ( layerInstances.Count == 0 ) continue;

			system.RegisterClutters( layerInstances.ToArray() );
			foreach ( var inst in layerInstances )
				layer.AddInstance( inst );

			if ( _useVolume )
			{
				system.RegisterVolumeInstances( _volume!.Id, layerInstances );
				layer.Parent.SerializeData();
			}
		}
	}

	/// <summary>
	/// Will preload all the objects (prefabs & models) in layers ahead of time that scatterer might use
	/// </summary>
	private void PreloadResources( IEnumerable<ClutterLayer> layers )
	{
		var paths = layers
			.SelectMany( l => l.Objects )
			.Select( o => o.Path )
			.Distinct();

		_resources.Preload( paths );
	}

	private void EraseInstances( ClutterSystem system, Vector3[] points )
	{
		var componentsToSerialize = new HashSet<ClutterComponent>();
		var instancesToErase = new List<ClutterInstance>();

		// For brush mode, get instances from all cells that overlap with brush bounds
		if ( _useBrush )
		{
			var brushBounds = new BBox(
				_brushPosition - new Vector3( _brushRadius ),
				_brushPosition + new Vector3( _brushRadius )
			);

			var overlappingCells = system.GetOverlappingCells( brushBounds, createIfMissing: false );
			foreach ( var cell in overlappingCells )
			{
				foreach ( var inst in cell.GetAllInstances() )
				{
					// Check if instance is within brush radius
					var distance = Vector3.DistanceBetween( _brushPosition, inst.transform.Position );
					if ( distance > _brushRadius )
						continue;

					// Check if instance belongs to one of our target layers
					foreach ( var layer in _layers )
					{
						bool isInLayer = layer.Instances.Any( i => i.InstanceId == inst.InstanceId );
						if ( isInLayer )
						{
							instancesToErase.Add( inst );
							break;
						}
					}
				}
			}
		}
		else
		{
			// Volume mode - erase based on generated points
			foreach ( var layer in _layers )
			{
				foreach ( var inst in layer.Instances.ToList() )
				{
					bool shouldErase = points.Any( p => (inst.transform.Position - p).Length <= _brushRadius );
					if ( shouldErase )
					{
						instancesToErase.Add( inst );
					}
				}
			}
		}

		// Erase instances
		foreach ( var instance in instancesToErase )
		{
			// Destroy prefab GameObjects
			ClutterComponent.DestroyInstance( instance );

			// Remove from layers and track which components need serialization
			foreach ( var layer in _layers )
			{
				var removed = layer.Instances.RemoveAll( i => i.InstanceId == instance.InstanceId );
				if ( removed > 0 )
				{
					componentsToSerialize.Add( layer.Parent );
				}
			}

			// Unregister from grid
			system.UnregisterClutter( instance );
		}

		// Update volume instances if using a volume
		if ( _useVolume )
		{
			system.RegisterVolumeInstances(
				_volume!.Id,
				[.. _layers.SelectMany( l => l.Instances )]
			);
		}

		// Serialize all modified components
		foreach ( var component in componentsToSerialize )
		{
			component.SerializeData();
		}
	}
}
