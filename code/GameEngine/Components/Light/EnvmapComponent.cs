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
	[Property] public int Resolution { get; set; } = 0;

	public override void DrawGizmos()
	{
		Gizmo.Draw.LineBBox( Bounds );
	}

	public void RenderDirty()
	{
		_sceneObject.RenderDirty();
	}

	public override void OnEnabled()
	{
		Assert.True( _sceneObject == null );
		Assert.NotNull( Scene );

		_sceneObject = new SceneCubemap( Scene.SceneWorld, Texture, Bounds );
		_sceneObject.Transform = Transform.World;
		_sceneObject.Projection = Projection;
		//_sceneObject.Texture = Texture;
		_sceneObject.TintColor = TintColor;
		_sceneObject.ProjectionBounds = Bounds;
		_sceneObject.LocalBounds = Bounds;
		_sceneObject.ZNear = 0.1f;
		_sceneObject.ZFar = 1000000.0f;

		_sceneObject.Tags.Add( "world" );
		_sceneObject.RenderDirty();
	}

	public override void OnDisabled()
	{
		_sceneObject?.Delete();
		_sceneObject = null;
	}

	int hash => HashCode.Combine( Transform, Texture, TintColor, Bounds, Projection, Resolution );
	int currentHash;

	protected override void OnPreRender()
	{
		if ( !_sceneObject.IsValid() )
			return;

		if ( hash == currentHash )
			return;

		currentHash = hash;

		_sceneObject.Transform = Transform.World;
		_sceneObject.Projection = Projection;
		//_sceneObject.Texture = Texture;
		_sceneObject.TintColor = TintColor;
		_sceneObject.ProjectionBounds = Bounds;
		_sceneObject.LocalBounds = Bounds;
	}

}
