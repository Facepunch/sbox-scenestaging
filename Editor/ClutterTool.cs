using Editor.TerrainEditor;
using Sandbox.Clutter;
using System;

namespace Sandbox;

[EditorTool( "clutter" )]
[Title( "Clutter Tool" )]
[Icon( "forest" )]
public sealed class ClutterTool : EditorTool
{
	BrushPreviewSceneObject brushPreview;
	ClutterList ClutterList;
	public BrushSettings BrushSettings { get; private set; } = new();
	[Property] public ClutterDefinition SelectedClutter { get; set; }

	private bool Erasing = false;
	private bool _dragging = false;
	private bool _painting = false;
	private Vector3 _lastPaintPosition;
	private float _paintDistanceThreshold = 50f;

	public override Widget CreateToolSidebar()
	{
		var sidebar = new ToolSidebarWidget();
		sidebar.AddTitle( "Clutter Brush Settings", "brush" );
		sidebar.MinimumWidth = 300;

		// Brush Properties
		{
			var group = sidebar.AddGroup( "Brush Properties" );
			var so = BrushSettings.GetSerialized();
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BrushSettings.Size ) ) ) );
			group.Add( ControlSheet.CreateRow( so.GetProperty( nameof( BrushSettings.Opacity ) ) ) );
		}

		// Clutter Selection
		{
			var group = sidebar.AddGroup( "Clutter Definitions", SizeMode.Flexible );
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
		if ( SelectedClutter?.Scatterer == null ) return;

		var tr = Scene.Trace.Ray( Gizmo.CurrentRay, 100000 )
			.UseRenderMeshes( true )
			.WithTag( "solid" )
			.Run();

		if ( !tr.Hit ) return;
		if ( _lastPaintPosition != Vector3.Zero && 
			Vector3.DistanceBetween( tr.HitPosition, _lastPaintPosition ) < _paintDistanceThreshold )
			return;

		_lastPaintPosition = tr.HitPosition;

		var system = Scene.GetSystem<ClutterGridSystem>();
		var brushRadius = BrushSettings.Size;
		var bounds = BBox.FromPositionAndSize( tr.HitPosition, brushRadius * 2 );

		if ( Erasing )
		{
			system.Erase( tr.HitPosition, brushRadius );
		}
		else
		{
			var instances = SelectedClutter.Scatterer.Scatter( bounds, SelectedClutter, Random.Shared.Next(), Scene );
			var count = (int)(instances.Count * BrushSettings.Opacity);

			foreach ( var instance in instances.Take( count ) )
			{
				// Paint both models and prefabs
				if ( instance.Entry != null && instance.Entry.HasAsset )
				{
					var t = instance.Transform;
					system.Paint( instance.Entry, t.Position, t.Rotation, t.Scale.x );
				}
			}
		}

		_painting = true;
	}

	private void OnPaintEnded()
	{
		_lastPaintPosition = Vector3.Zero;
		
		if ( _painting )
		{
			var system = Scene.GetSystem<ClutterGridSystem>();
			system.Flush();
		}
		
		_painting = false;
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
}

