using Sandbox;
using Sandbox.Diagnostics;
using System.Numerics;

[Title( "New Decal" )]
[Category( "Rendering" )]
[Icon( "lens_blur" )]
[EditorHandle( "materials/gizmo/decal.png" )]
public sealed class NewDecal : Component, Component.ExecuteInEditor
{
	/// <summary>
	/// The size of the decal. x being width, y being the height, z being the projection distance
	/// </summary>
	[Property, MakeDirty] public Vector3 Size { get; set; } = new Vector3( 32, 32, 32 );
	[Property, Group( "Textures" )] public Texture Color { get; set; }

	DecalSceneObject _sceneObject;

	protected override void OnEnabled()
	{
		Assert.IsNull( _sceneObject );
		_sceneObject = new DecalSceneObject( Scene.SceneWorld );

		_sceneObject.ColorTexture = Color;
		_sceneObject.Size = Size;

		OnTransformChanged();
		Transform.OnTransformChanged += OnTransformChanged;
	}

	protected override void OnDirty()
	{
		_sceneObject.Size = Size;
	}

	protected override void OnDisabled()
	{
		Transform.OnTransformChanged -= OnTransformChanged;

		_sceneObject?.Delete();
		_sceneObject = null;
	}

	private void OnTransformChanged()
	{
		if ( _sceneObject.IsValid() )
			_sceneObject.Transform = Transform.World;
	}


	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( Gizmo.IsSelected ? 1.0f : 0.2f );
		Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( Vector3.Zero, Size * 2 ) );

		var bbox = BBox.FromPositionAndSize( Vector3.Zero, Size );

		if ( Gizmo.Control.BoundingBox( "Size", bbox, out var bbbox ) )
		{
			Size = bbbox.Size;
		}
	}
}
