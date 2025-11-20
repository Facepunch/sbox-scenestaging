using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

/// <summary>
/// Game object system that manages a spatial grid for clutter placement.
/// Automatically discovers and manages ClutterComponent instances in the scene.
/// </summary>
public sealed class ClutterGridSystem : GameObjectSystem
{
	private Dictionary<ClutterComponent, ClutterLayer> ComponentToLayer { get; set; } = new();

	public ClutterGridSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 0, OnUpdate, "ClutterGridSystem.Update" );
	}

	private void OnUpdate()
	{
		var camera = Scene.IsEditor
			? Scene.GetAllObjects( true ).FirstOrDefault( x => x.Name == "editor_camera" )?.Components.Get<CameraComponent>()
			: Scene.Camera;

		if ( camera == null )
			return;

		// Find all ClutterComponents in the scene
		var components = Scene.GetAllComponents<ClutterComponent>()
			.Where( c => c.Enabled && c.Infinite )
			.ToList();

		// Remove layers for components that no longer exist or are disabled
		var componentsToRemove = ComponentToLayer.Keys
			.Where( c => !components.Contains( c ) )
			.ToList();

		foreach ( var component in componentsToRemove )
		{
			ComponentToLayer[component].ClearAllTiles();
			ComponentToLayer.Remove( component );
		}

		// Update or create layers for active components
		foreach ( var component in components )
		{
			var settings = component.GetCurrentSettings();
			
			if ( !settings.IsValid )
				continue;

			// Get or create layer for this component
			if ( !ComponentToLayer.TryGetValue( component, out var layer ) )
			{
				layer = new ClutterLayer( settings, component.GameObject );
				ComponentToLayer[component] = layer;
			}

			// Update layer with current settings and camera position
			layer.UpdateSettings( settings );
			layer.UpdateTiles( camera.WorldPosition );
		}
	}
}
