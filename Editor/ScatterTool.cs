using Editor;
using Editor.TerrainEditor;
using System;
using System.Linq;

namespace Sandbox;

[EditorTool( "scatter" )]
[Title( "Scatter Tool" )]
[Icon( "forest" )]
public sealed class ScatterTool : EditorTool
{
	BrushPreviewSceneObject brushPreview;
	ClutterList ClutterList;
	public BrushSettings BrushSettings { get; private set; } = new();
	[Property] public ClutterDefinition SelectedClutter { get; set; }

	private bool Erasing = false;
	private bool _dragging = false;
	private Vector3 _lastPaintPosition;
	private float _paintDistanceThreshold = 50f;

	public override Widget CreateToolSidebar()
	{
		var sidebar = new ToolSidebarWidget();
		sidebar.AddTitle( "Brush Settings", "brush" );
		sidebar.MinimumWidth = 300;

		// Brush Properties
		{
			var group = sidebar.AddGroup( "Brush Properties" );
			var so = BrushSettings.GetSerialized();
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BrushSettings.Size ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BrushSettings.Opacity ) ) ) );
		}

		// clutter Selection
		{
			var group = sidebar.AddGroup( "clutters", SizeMode.Flexible );
			ClutterList = new ClutterList( sidebar );
			ClutterList.MinimumHeight = 300;
			ClutterList.OnclutterSelected = ( clutter ) =>
			{
				SelectedClutter = clutter;
			};

			ClutterList.BuildItems();
			group.Add( ClutterList );

		}
		
		return sidebar;
	}

	public override void OnUpdate()
	{
		var ctlrHeld = Gizmo.IsCtrlPressed;
		if ( Gizmo.IsCtrlPressed && !Erasing )
		{
			Erasing = true;
		}

		DrawBrushPreview();

		Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( Vector3.Zero, 999999 ) );

		if ( Gizmo.IsLeftMouseDown )
		{
			if ( !_dragging )
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

		if ( !ctlrHeld )
		{
			Erasing = false;
		}
	}

	public override void OnDisabled()
	{
		brushPreview?.Delete();
	}

	private void OnPaintBegin()
	{
		_lastPaintPosition = Vector3.Zero;
	}

	private void OnPaintUpdate()
	{
		if ( SelectedClutter == null || SelectedClutter.Scatterer == null )
			return;

		var tr = Scene.Trace.Ray( Gizmo.CurrentRay, 100000 )
			.UseRenderMeshes( true )
			.WithTag( "solid" )
			.WithoutTags( "scattered_object" )
			.Run();

		if ( !tr.Hit )
			return;

		if ( _lastPaintPosition != Vector3.Zero && Vector3.DistanceBetween( tr.HitPosition, _lastPaintPosition ) < _paintDistanceThreshold )
			return;

		_lastPaintPosition = tr.HitPosition;

		if ( Erasing )
		{
			EraseScatteredObjects( tr.HitPosition );
		}
		else
		{
			ScatterAtPosition( tr.HitPosition );
		}
	}

	private void OnPaintEnded()
	{
		_lastPaintPosition = Vector3.Zero;
	}

	private void DrawBrushPreview()
	{
		var tr = Scene.Trace.Ray( Gizmo.CurrentRay, 50000 )
			.UseRenderMeshes( true )
			.WithTag( "solid" )
			.WithoutTags( "scattered_object" )
			.Run();

		if ( !tr.Hit )
			return;

		brushPreview ??= new BrushPreviewSceneObject( Gizmo.World );

		var brushRadius = BrushSettings.Size;
		var color = Erasing ? Color.FromBytes( 250, 150, 150 ) : Color.FromBytes( 150, 150, 250 );
		color.a = BrushSettings.Opacity;

		var brush = TerrainEditorTool.Brush;
		var previewPosition = tr.HitPosition + tr.Normal * 1f;
		var surfaceRotation = Rotation.LookAt( tr.Normal );

		brushPreview.RenderLayer = SceneRenderLayer.OverlayWithDepth;
		brushPreview.Bounds = BBox.FromPositionAndSize( 0, float.MaxValue );
		brushPreview.Transform = new Transform( previewPosition, surfaceRotation );
		brushPreview.Radius = brushRadius;
		brushPreview.Texture = brush?.Texture;
		brushPreview.Color = color;
	}

	private void ScatterAtPosition( Vector3 position )
	{
		if ( SelectedClutter == null || SelectedClutter.Scatterer == null )
			return;

		var brushRadius = BrushSettings.Size;
		var bounds = new BBox(
			position + new Vector3( -brushRadius, -brushRadius, -100 ),
			position + new Vector3( brushRadius, brushRadius, 100 )
		);

		var seed = System.DateTime.Now.Ticks.GetHashCode();
		var instances = SelectedClutter.Scatterer.Scatter( bounds, SelectedClutter, seed );

		int targetCount = (int)MathF.Ceiling( instances.Count * BrushSettings.Opacity );
		var filteredInstances = instances.Take( targetCount ).ToList();

		foreach ( var instance in filteredInstances )
		{
			if ( instance.Entry.Prefab != null )
			{
				var spawnedObject = instance.Entry.Prefab.Clone();
				spawnedObject.WorldTransform = instance.Transform;
				spawnedObject.Tags.Add( "scattered_object" );
			}
		}
	}

	private void EraseScatteredObjects( Vector3 position )
	{
		var brushRadius = BrushSettings.Size;
		var allObjects = SceneEditorSession.Active?.Scene?.GetAllObjects( false ) ?? [];
		
		var objectsToErase = allObjects
			.Where( obj => obj.Tags.Has( "scattered_object" ) )
			.Where( obj => Vector3.DistanceBetween( obj.WorldPosition, position ) <= brushRadius )
			.ToList();

		foreach ( var obj in objectsToErase )
		{
			obj.Destroy();
		}
	}
}
