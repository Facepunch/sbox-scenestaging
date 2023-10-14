using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Envmap Probe" )]
[Category( "Light" )]
[Icon( "light_mode", "red", "white" )]
[EditorHandle( "materials/gizmo/envmap.png" )]
public class EnvmapComponent : BaseComponent, BaseComponent.ExecuteInEditor
{
	SceneCubemap _sceneObject;

	[Property] public SceneCubemap.ProjectionMode Projection { get; set; }
	[Property] public Color TintColor { get; set; } = Color.White;
	[Property] public Texture Texture { get; set; }
	[Property] public BBox Bounds { get; set; } = new BBox( 0, 1024 );

	public override void DrawGizmos()
	{
		Gizmo.Draw.LineBBox( Bounds );
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		Texture ??= Texture.Load( "textures/cubemaps/default2.vtex" );
		_sceneObject = new SceneCubemap( Scene.SceneWorld, Texture, Bounds );
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		_sceneObject.Transform = Transform.World;
		_sceneObject.Projection = Projection;
		_sceneObject.Texture = Texture;
		_sceneObject.TintColor = TintColor;
		_sceneObject.ProjectionBounds = Bounds;
		_sceneObject.LocalBounds = Bounds;
	}

}
