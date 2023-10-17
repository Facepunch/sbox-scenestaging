using Sandbox;
using System;

[Title( "Sharpen" )]
[Category( "Post Processing" )]
[Icon( "deblur" )]
public sealed class Sharpen : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Range( 0, 5 )]
	[Property] public float Scale { get; set; } = 2;

	IDisposable renderHook;

	public override void OnEnabled()
	{
		renderHook?.Dispose();

		var cc = GetComponent<CameraComponent>( false, false );
		renderHook = cc.AddHookBeforeOverlay( "Pixelate", 500, RenderEffect );
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

		if ( Scale == 0.0f )
			return;

		attributes.Set( "standard.sharpen.strength", Scale );

		Graphics.GrabFrameTexture( "ColorBuffer", attributes );
		Graphics.GrabDepthTexture( "DepthBuffer", attributes );
		Graphics.Blit( Material.Load( "materials/postprocess/standard_pass1.vmat" ), attributes );
	}

}
