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
	IsotopeList isotopeList;

	[Property] public float BrushSize { get; set; } = 1f;
	[Property] public float BrushOpacity { get; set; } = 0.5f;
	[Property] public ClutterIsotope SelectedIsotope { get; set; }

	private bool Erasing = false;
	private bool _dragging = false;
	private Vector3 _lastPaintPosition;
	private float _paintDistanceThreshold = 50f;

	public override Widget CreateToolWidget()
	{
		var widget = new Widget( null );
		widget.Layout = Layout.Column();
		widget.Layout.Margin = 8;
		widget.Layout.Spacing = 8;
		widget.MinimumWidth = 250;
		widget.MaximumWidth = 300;

		var brushGroupLabel = new Label( "Brush Settings" );
		brushGroupLabel.SetStyles( "font-weight: bold; font-size: 14px; margin-bottom: 4px;" );
		widget.Layout.Add( brushGroupLabel );

		var sizeSheet = new ControlSheet();
		sizeSheet.AddRow( this.GetSerialized().GetProperty( nameof( BrushSize ) ) );
		widget.Layout.Add( sizeSheet );

		var opacitySheet = new ControlSheet();
		opacitySheet.AddRow( this.GetSerialized().GetProperty( nameof( BrushOpacity ) ) );
		widget.Layout.Add( opacitySheet );

		var separator = new Widget( null );
		separator.FixedHeight = 1;
		separator.SetStyles( "background-color: rgba(255, 255, 255, 0.2); margin: 8px 0px;" );
		widget.Layout.Add( separator );

		var isotopeGroupLabel = new Label( "Select Isotope" );
		isotopeGroupLabel.SetStyles( "font-weight: bold; font-size: 14px; margin-bottom: 8px;" );
		widget.Layout.Add( isotopeGroupLabel );

		isotopeList = new IsotopeList( null );
		isotopeList.MinimumHeight = 300;
		isotopeList.OnIsotopeSelected = ( isotope ) =>
		{
			SelectedIsotope = isotope;
		};
		widget.Layout.Add( isotopeList );

		widget.Layout.AddStretchCell();

		return widget;
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
		if ( SelectedIsotope == null || SelectedIsotope.Scatterer == null )
			return;

		var tr = Scene.Trace.Ray( Gizmo.CurrentRay, 10000 )
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
			ScatterAtPosition( tr.HitPosition, tr.Normal );
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

		var brushRadius = BrushSize * 50f;
		var color = Erasing ? Color.FromBytes( 250, 150, 150 ) : Color.FromBytes( 150, 150, 250 );
		color.a = BrushOpacity;

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

	private void ScatterAtPosition( Vector3 position, Vector3 normal )
	{
		if ( SelectedIsotope == null || SelectedIsotope.Scatterer == null )
			return;

		var brushRadius = BrushSize * 50f;
		var bounds = new BBox(
			position + new Vector3( -brushRadius, -brushRadius, -100 ),
			position + new Vector3( brushRadius, brushRadius, 100 )
		);

		var seed = System.DateTime.Now.Ticks.GetHashCode();
		var instances = SelectedIsotope.Scatterer.Scatter( bounds, SelectedIsotope, seed );

		int targetCount = (int)MathF.Ceiling( instances.Count * BrushOpacity );
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
		var brushRadius = BrushSize * 50f;
		var allObjects = SceneEditorSession.Active?.Scene?.GetAllObjects( false ) ?? Enumerable.Empty<GameObject>();
		
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
