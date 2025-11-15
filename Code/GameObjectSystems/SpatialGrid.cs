using Sandbox;
using System;
using System.Collections.Generic;

namespace SceneStaging;

/// <summary>
/// Spatial grid that automatically tracks components implementing Component.SpatialGrid.
/// Performant event-driven architecture using Transform.OnTransformChanged.
/// </summary>
public class SpatialGrid : GameObjectSystem<SpatialGrid>
{
	public float CellSize { get; set; } = 2048f;

	private Dictionary<string, List<Sandbox.Component>> _cells = new();
	private Dictionary<Sandbox.Component, ComponentState> _tracked = new();

	public SpatialGrid( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 0, OnSceneLoaded, "SpatialGridSceneLoaded" );
		Listen( Stage.FinishUpdate, 0, OnUpdate, "SpatialGridUpdate" );
	}

	private void OnSceneLoaded()
	{
		RegisterAllComponents();
	}

	private void OnUpdate()
	{
		RegisterAllComponents();
	}

	private void RegisterAllComponents()
	{
		// Find all Component.SpatialGrid components and register new ones
		foreach ( var component in Scene.GetAllComponents<Sandbox.Component.SpatialGrid>() )
		{
			var comp = component as Sandbox.Component;
			if ( comp != null && comp.Enabled )
			{
				Register( comp );
			}
		}

		// Unregister destroyed/disabled components
		var toRemove = new List<Sandbox.Component>();
		foreach ( var tracked in _tracked.Keys )
		{
			if ( !tracked.IsValid || !tracked.Enabled )
			{
				toRemove.Add( tracked );
			}
		}

		foreach ( var comp in toRemove )
		{
			Unregister( comp );
		}
	}

	public void Register( Sandbox.Component component )
	{
		if ( component == null || _tracked.ContainsKey( component ) )
			return;

		var pos = component.Transform.Position;
		var cellKey = GetCellKey( GetCellPosition( pos ) );
		var cell = GetOrCreateCell( cellKey );

		cell.Add( component );

		// Create state and subscribe to transform changes
		var state = new ComponentState
		{
			LastPosition = pos,
			OnChanged = () => OnTransformChanged( component )
		};

		component.Transform.OnTransformChanged += state.OnChanged;
		_tracked[component] = state;
	}

	public void Unregister( Sandbox.Component component )
	{
		if ( !_tracked.TryGetValue( component, out var state ) )
			return;

		// Unsubscribe from transform
		component.Transform.OnTransformChanged -= state.OnChanged;

		// Remove from cell
		var cellKey = GetCellKey( GetCellPosition( state.LastPosition ) );
		if ( _cells.TryGetValue( cellKey, out var cell ) )
		{
			cell.Remove( component );
		}

		_tracked.Remove( component );
	}

	private void OnTransformChanged( Sandbox.Component component )
	{
		if ( !_tracked.TryGetValue( component, out var state ) )
			return;

		var newPos = component.Transform.Position;
		var oldCellPos = GetCellPosition( state.LastPosition );
		var newCellPos = GetCellPosition( newPos );

		if ( oldCellPos != newCellPos )
		{
			var oldCellKey = GetCellKey( oldCellPos );
			var newCellKey = GetCellKey( newCellPos );

			if ( _cells.TryGetValue( oldCellKey, out var oldCell ) )
			{
				oldCell.Remove( component );
			}

			var newCell = GetOrCreateCell( newCellKey );
			newCell.Add( component );
		}

		state.LastPosition = newPos;
	}

	private Vector2 GetCellPosition( Vector3 pos )
	{
		return new Vector2(
			(int)MathF.Floor( pos.x / CellSize ),
			(int)MathF.Floor( pos.y / CellSize )
		);
	}

	private string GetCellKey( Vector2 cellPos )
	{
		return cellPos.GetHashCode().ToString();
	}

	private List<Sandbox.Component> GetOrCreateCell( string cellKey )
	{
		if ( !_cells.TryGetValue( cellKey, out var cell ) )
		{
			cell = new List<Sandbox.Component>();
			_cells[cellKey] = cell;
		}

		return cell;
	}

	private class ComponentState
	{
		public Vector3 LastPosition;
		public Action OnChanged;
	}
}
