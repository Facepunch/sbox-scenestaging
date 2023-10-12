using Sandbox;
using Sandbox.Diagnostics;

[Title( "VolumetricFogVolume" )]
[Category( "Rendering" )]
[Icon( "visibility", "red", "white" )]
[EditorHandle( "materials/gizmo/VolumetricFogVolume.png" )]
public class VolumetricFogVolume : BaseComponent
{
	SceneFogVolume sceneObject;

	[Property] public BBox Bounds { get; set; } = BBox.FromPositionAndSize( 0, 300 );
	[Property] public float Strength { get; set; } = 2.0f;
	[Property] public float FalloffExponent { get; set; } = 1.0f;

	public override void OnEnabled()
	{
		Assert.True( sceneObject == null );
		Assert.NotNull( Scene );

		sceneObject = new SceneFogVolume( Scene.SceneWorld, Transform.World, Bounds, Strength, FalloffExponent );
	}

	public override void OnDisabled()
	{
		sceneObject?.Delete();
		sceneObject = null;
	}

	protected override void OnPreRender()
	{
		if ( !sceneObject.IsValid() )
			return;

		sceneObject.Transform = Transform.World;
		sceneObject.BoundingBox = Bounds;
		sceneObject.FogStrength = Strength;
		sceneObject.FalloffExponent = FalloffExponent;
	}

}
