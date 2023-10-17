using Sandbox;
using System;

[Title( "FilmGrain" )]
[Category( "Post Processing" )]
[Icon( "grain" )]
public sealed class FilmGrain : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property] public float Intensity { get; set; } = 0.1f;

	[Property] public float Response { get; set; } = 0.5f;

	IDisposable renderHook;

	public override void OnEnabled()
	{
		renderHook?.Dispose();

		var cc = GetComponent<CameraComponent>( false, false );
		renderHook = cc.AddHookBeforeOverlay( "Film Grain", 1000, RenderEffect );
	}

	public override void OnDisabled()
	{
		renderHook?.Dispose();
		renderHook = null;
	}

	RenderAttributes attributes = new RenderAttributes();

	public void RenderEffect( SceneCamera camera )
	{
		if ( !camera.EnablePostProcessing )
			return;

		if ( Intensity.AlmostEqual( 0.0f ) )
			return;

		attributes.Set( "standard.grain.intensity", Intensity );
		attributes.Set( "standard.grain.response", Response );

		Graphics.GrabFrameTexture( "ColorBuffer", attributes );
		Graphics.GrabDepthTexture( "DepthBuffer", attributes );
		Graphics.Blit( Material.Load( "materials/postprocess/standard_pass3.vmat" ), attributes );
	}

}
