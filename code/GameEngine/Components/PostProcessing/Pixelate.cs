using Sandbox;
using System;

[Title( "Pixelate" )]
[Category( "Post Processing" )]
[Icon( "apps" )]
public sealed class Pixelate : BaseComponent, BaseComponent.ExecuteInEditor
{
	[Property] public float Scale { get; set; } = 5;

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

		attributes.Set( "standard.pixelate.pixelation", Scale );

		Graphics.GrabFrameTexture( "ColorBuffer", attributes );
		Graphics.GrabDepthTexture( "DepthBuffer", attributes );
		Graphics.Blit( Material.Load( "materials/postprocess/standard_pass1.vmat" ), attributes );
	}

}
