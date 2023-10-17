using Sandbox;
using Sandbox.UI;
using System;
using static Sandbox.RenderHook;

[Title( "Depth Of Field" )]
[Category( "Post Processing" )]
[Icon( "center_focus_strong" )]
public sealed class DepthOfField : BaseComponent, BaseComponent.ExecuteInEditor
{
	RenderAttributes attributes = new RenderAttributes();

	/// <summary>
	/// How far away from the camera to focus
	/// </summary>
	[Range(0, 1000)]
	[Property] public float FocalDistance { get; set; } = 200.0f;

	/// <summary>
	/// How blurry to make stuff that isn't in focus
	/// </summary>
	[Range( 0, 100 )]
	[Property] public float BlurSize { get; set; } = 100.0f;

	/// <summary>
	/// Should we blur what's ahead the focal point towards us?
	/// </summary>
	[Property] public bool FrontBlur { get; set; } = false;

	/// <summary>
	/// Should we blur what's behind the focal point?
	/// </summary>
	[Property] public bool BackBlur { get; set; } = true;

	IDisposable renderHook;

	public override void OnEnabled()
	{
		renderHook?.Dispose();
		renderHook = null;

		var cc = GetComponent<CameraComponent>( false, false );
		renderHook = cc.AddHookAfterTransparent( "Depth Of Field", 100, RenderEffect );
	}

	public override void OnDisabled()
	{
		renderHook?.Dispose();
		renderHook = null;
	}

	private enum DoFPass
	{
		Blur,
		CombineFront,
		CombineBack,
	};

	/// <summary>
	/// Render the effect onto given scene camera.
	/// </summary>
	public void RenderEffect( SceneCamera camera )
	{
		if ( !camera.EnablePostProcessing )
			return;

		if ( !BackBlur && !FrontBlur )
			return;

		var material = Material.FromShader( "postprocess_standard_dof.vfx" );

		int blurDownscale = (int)((1.0 / FocalDistance) * (BlurSize));

		if ( FrontBlur )
			blurDownscale--; // Sharpen it a bit if we have front blur

		if ( blurDownscale < 2 ) blurDownscale = 2;

		Graphics.GrabFrameTexture( "ColorBuffer", attributes );
		Graphics.GrabDepthTexture( "DepthBuffer", attributes );

		attributes.Set( "standard.dof.focusplane", (float)FocalDistance );
		attributes.Set( "standard.dof.radius", BlurSize );
		attributes.Set( "standard.dof.blurdownscale", blurDownscale );

		using var BlurTexture = RenderTarget.GetTemporary( blurDownscale, ImageFormat.RGBA16161616F, ImageFormat.None );

		// Pass Blur
		{
			attributes.SetCombo( "D_DOF_PASS", DoFPass.Blur );
			Graphics.RenderTarget = BlurTexture;
			Graphics.Blit( material, attributes );
		}

		// Composite back blur pass
		if ( BackBlur )
		{
			attributes.Set( "BackBlur", BlurTexture.ColorTarget );
			attributes.SetCombo( "D_DOF_PASS", DoFPass.CombineBack );
			Graphics.RenderTarget = null;
			Graphics.Blit( material, attributes );
		}

		// Composite front blur pass
		if ( FrontBlur )
		{
			attributes.Set( "BackBlur", BlurTexture.ColorTarget );
			attributes.SetCombo( "D_DOF_PASS", DoFPass.CombineFront );
			Graphics.RenderTarget = null;
			Graphics.Blit( material, attributes );
		}
	}
}
