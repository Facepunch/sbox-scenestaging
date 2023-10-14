using Sandbox;

// TODO - requires camera component
[Title( "Tonemapping" )]
[Category( "Camera" )]
[Icon( "exposure" )]
public class Tonemapping : BaseComponent, CameraComponent.ISceneCameraSetup
{
	[Property]
	public float MinimumExposure { get; set; } = 1.0f;

	[Property]
	public float MaximumExposure { get; set; } = 2.0f;

	[Property]
	public float ExposureCompensation { get; set; } = 0.0f;

	[Property]
	public float Rate { get; set; } = 1.0f;

	public void SetupCamera( CameraComponent camera, SceneCamera sceneCamera )
	{
		sceneCamera.Tonemap.Enabled = true;
		sceneCamera.Tonemap.ExposureCompensation = ExposureCompensation;
		sceneCamera.Tonemap.MinExposure = MinimumExposure;
		sceneCamera.Tonemap.MaxExposure = MaximumExposure;
		sceneCamera.Tonemap.Rate = Rate;

		// this Fade value is a multiple of Rate, and allows you to control the
		// speed of decent. I'm not enabling yet because it's confusing. I've found
		// that 16 here makes it kind of match the up rate. We should look into this
		// and give distinct up and down rates.
		sceneCamera.Tonemap.Fade = 16.0f;
	}

}
