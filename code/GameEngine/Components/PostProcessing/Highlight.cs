using Sandbox;
using Sandbox.Component;
using System;
using System.Linq;

/// <summary>
/// This should be added to a camera that you want to outline stuff
/// </summary>
[Title( "Highlight" )]
[Category( "Post Processing" )]
[Icon( "lightbulb_outline" )]
public sealed class Highlight : BaseComponent, BaseComponent.ExecuteInEditor
{
	IDisposable renderHook;

	public override void OnEnabled()
	{
		renderHook?.Dispose();

		var cc = GetComponent<CameraComponent>( false, false );
		renderHook = cc.AddHookAfterTransparent( "Highlight", 1000, RenderEffect );
	}

	public override void OnDisabled()
	{
		renderHook?.Dispose();
		renderHook = null;
	}

	enum OutlinePass
	{
		Inside,
		Outside,
	}

	RenderAttributes attributes = new RenderAttributes();

	public void RenderEffect( SceneCamera camera )
	{
		var outlines = Scene.FindAllComponents<HighlightOutline>();

		if ( !outlines.Any() ) return;

		// Copy the depth buffer once
		Graphics.GrabFrameTexture( "ColorTexture" );
		Graphics.GrabDepthTexture( "DepthTexture" );

		// Generate a temporary render target to draw the stencil to, also so we don't clash with the main depth buffer
		using var rt = RenderTarget.GetTemporary( 1, ImageFormat.None, ImageFormat.D24S8, -1 );
		Graphics.RenderTarget = rt;

		Graphics.Clear( Color.Black, false, true, true );

		// Draw the stencil first before drawing the outside outline
		foreach ( var glow in outlines ) { DrawGlow( glow, OutlinePass.Inside ); }
		foreach ( var glow in outlines ) { DrawGlow( glow, OutlinePass.Outside ); }

		Graphics.RenderTarget = null;
	}

	private static void DrawGlow( HighlightOutline glow, OutlinePass pass )
	{
		foreach ( var model in glow.GetComponents<ModelComponent>( true, true ) )
		{
			var so = model.SceneObject;
			if ( so is null ) continue;

			var shapeMat = glow.Material ?? Material.FromShader( "postprocess/objecthighlight/objecthighlight.shader" );

			// Inside glow and stencil
			if ( pass == OutlinePass.Inside )
			{
				Graphics.Attributes.Set( "D_OUTLINE_PASS", (int)OutlinePass.Inside );
				Graphics.Attributes.Set( "Color", glow.InsideColor );
				Graphics.Attributes.Set( "ObscuredColor", glow.InsideObscuredColor );

				Graphics.Render( so, material: shapeMat );
			}

			// Outside glow
			if ( glow.Width > 0.0f && pass == OutlinePass.Outside && (glow.Color != Color.Transparent || glow.ObscuredColor != Color.Transparent) )
			{
				Graphics.Attributes.Set( "D_OUTLINE_PASS", (int)OutlinePass.Outside );
				Graphics.Attributes.Set( "Color", glow.Color );
				Graphics.Attributes.Set( "ObscuredColor", glow.ObscuredColor );
				Graphics.Attributes.Set( "LineWidth", glow.Width );

				Graphics.Render( so, material: shapeMat );
			}
		}
	}

}

/// <summary>
/// This component should be added to stuff you want to be outlined
/// </summary>
[Title( "Highlight Outline" )]
[Category( "Renderering" )]
[Icon( "lightbulb_outline" )]
public class HighlightOutline : BaseComponent
{
	/// <summary>
	/// If defined, the glow will use this material rather than a generated one.
	/// </summary>
	[Property] public Material Material { get; set; }

	/// <summary>
	/// The colour of the glow outline
	/// </summary>
	[Property] public Color Color { get; set; } = Color.White;

	/// <summary>
	/// The colour of the glow when the mesh is obscured by something closer.
	/// </summary>
	[Property] public Color ObscuredColor { get; set; } = Color.Black * 0.4f;

	/// <summary>
	/// Color of the inside of the glow
	/// </summary>
	[Property] public Color InsideColor { get; set; } = Color.Transparent;

	/// <summary>
	/// Color of the inside of the glow when the mesh is obscured by something closer.
	/// </summary>
	[Property] public Color InsideObscuredColor { get; set; } = Color.Transparent;

	/// <summary>
	/// The width of the line of the glow
	/// </summary>
	[Property] public float Width { get; set; } = 0.25f;
}
