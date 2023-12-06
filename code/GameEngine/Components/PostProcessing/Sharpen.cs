using Sandbox;
using System;

[Title( "Sharpen" )]
[Category( "Post Processing" )]
[Icon( "deblur" )]
public sealed class Sharpen : Component, Component.ExecuteInEditor
{
	[Range( 0, 5 )]
	[Property] public float Scale { get; set; } = 2;

	IDisposable renderHook;

	protected override void OnEnabled()
	{
		renderHook?.Dispose();

		var cc = Components.Get<CameraComponent>( true );
		renderHook = cc.AddHookBeforeOverlay( "Pixelate", 500, RenderEffect );
	}

	protected override void OnDisabled()
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
