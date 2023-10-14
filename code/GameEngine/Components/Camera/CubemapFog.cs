using Sandbox;

// TODO - requires camera component
[Title( "Cubemap Fog" )]
[Category( "Camera" )]
[Icon( "foggy" )]
public class CubemapFog : BaseComponent, CameraComponent.ISceneCameraSetup
{
	[Property]
	public float StartDistance { get; set; } = 10.0f;

	[Property]
	public float EndDistance { get; set; } = 4096.0f;

	[Property]
	public float FalloffExponent { get; set; } = 1.0f;

	[Property]
	public Material Sky { get; set; } = Material.Load( "materials/skybox/light_test_sky_sunny02.vmat" );

	[Property]
	public float Blur { get; set; } = 0.5f;

	public void SetupCamera( CameraComponent camera, SceneCamera sceneCamera )
	{
		// garry: I don't like making them select a texture, so I try to get the texture
		// from a skybox material.
		var tex = Sky?.GetTexture( "g_tSkyTexture" );

		sceneCamera.CubemapFog.Enabled = tex is not null;
		sceneCamera.CubemapFog.Texture = tex;
		sceneCamera.CubemapFog.StartDistance = StartDistance;
		sceneCamera.CubemapFog.EndDistance = EndDistance;
		sceneCamera.CubemapFog.FalloffExponent = FalloffExponent;
		sceneCamera.CubemapFog.LodBias = 1 - Blur;
		sceneCamera.CubemapFog.HeightStart = 0f;
		sceneCamera.CubemapFog.HeightWidth = 0f;
		sceneCamera.CubemapFog.HeightExponent = 1f;
		sceneCamera.CubemapFog.Transform = global::Transform.Zero;
	}

}
