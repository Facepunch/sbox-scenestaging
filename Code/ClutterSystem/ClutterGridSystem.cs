using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sandbox;

/// <summary>
/// Game object system that manages clutter generation.
/// Handles infinite streaming layers and executes generation jobs.
/// </summary>
public sealed class ClutterGridSystem : GameObjectSystem
{
	private Dictionary<ClutterComponent, ClutterLayer> ComponentToLayer { get; set; } = new();
	private Queue<ClutterGenerationJob> JobQueue { get; set; } = new();
	private List<Task> ActiveJobs { get; set; } = new();

	public ClutterGridSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ClutterGridSystem.Update" );
	}

	/// <summary>
	/// Queues a generation job for processing.
	/// </summary>
	public void QueueJob( ClutterGenerationJob job )
	{
		if ( job?.Isotope?.Scatterer == null || job.ParentObject == null )
			return;

		JobQueue.Enqueue( job );
	}

	private void OnUpdate()
	{
		UpdateInfiniteLayers();
		ProcessJobs();
	}

	private void UpdateInfiniteLayers()
	{
		var camera = Scene.IsEditor
			? Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "editor_camera" )?.Components.Get<CameraComponent>()
			: Scene.Camera;

		if ( camera == null )
			return;

		var components = Scene.GetAllComponents<ClutterComponent>()
			.Where( c => c.Enabled && c.Infinite )
			.ToList();

		var componentsToRemove = ComponentToLayer.Keys
			.Where( c => !components.Contains( c ) )
			.ToList();

		foreach ( var component in componentsToRemove )
		{
			ComponentToLayer[component].ClearAllTiles();
			ComponentToLayer.Remove( component );
		}

		foreach ( var component in components )
		{
			var settings = component.GetCurrentSettings();
			
			if ( !settings.IsValid )
				continue;

			if ( !ComponentToLayer.TryGetValue( component, out var layer ) )
			{
				layer = new ClutterLayer( settings, component.GameObject );
				ComponentToLayer[component] = layer;
			}

			layer.UpdateSettings( settings );
			
			// Get jobs from layer and queue them
			var jobs = layer.UpdateTiles( camera.WorldPosition );
			foreach ( var job in jobs )
			{
				QueueJob( job );
			}
		}
	}

	private void ProcessJobs()
	{
		const int MAX_CONCURRENT_JOBS = 8;
		
		// Remove completed jobs
		ActiveJobs.RemoveAll( job => job.IsCompleted );
		
		// Start new jobs up to the limit
		while ( ActiveJobs.Count < MAX_CONCURRENT_JOBS && JobQueue.Count > 0 )
		{
			var job = JobQueue.Dequeue();
			var task = job.ExecuteAsync();
			ActiveJobs.Add( task );
		}
	}
}
