using Editor.TerrainEditor;

namespace Sandbox;

/// <summary>
/// Editor tool for manually scattering clutter in the scene.
/// </summary>
[EditorTool( "terrain.scatter" )]
[Title( "Scatter Tool" )]
[Icon( "forest" )]
public sealed class ScatterTool : EditorTool
{
	BrushPreviewSceneObject brushPreview;

	private float BrushSize = 1f;
	private float BrushOpacity = 0.5f;
	private bool Erasing = false;

	private bool _dragging = false;
	public ScatterTool()
	{

	}

	public override void OnUpdate()
	{
		var ctlrHeld = Gizmo.IsCtrlPressed;
		if ( Gizmo.IsCtrlPressed && !Erasing )
		{
			Erasing = true;
		}

		DrawBrushPreview();

		// Hitbox to capture all mouse events and prevent entity selection
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

		// Restore erase mode after ctrl is released
		if ( !ctlrHeld )
		{
			Erasing = false;
		}
	}

	public override void OnEnabled()
	{
	}

	public override void OnDisabled()
	{
		brushPreview?.Delete();
	}

	private void OnPaintBegin()
	{
	}

	private void OnPaintUpdate()
	{
	}

	private void OnPaintEnded()
	{
	}

	private void DrawBrushPreview()
	{
		var tr = Trace.UseRenderMeshes( true ).WithTag( "solid" ).WithoutTags( "scattered_object" ).Run();
		if ( !tr.Hit )
			return;

		brushPreview ??= new BrushPreviewSceneObject( Gizmo.World );

		var brushRadius = BrushSize * 50f;

		// Set brush color, red for erase, blue for scatter
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

}
