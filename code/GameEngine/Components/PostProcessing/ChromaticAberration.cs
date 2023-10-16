using Sandbox;
using System;

[Title( "Chromatic Aberration" )]
[Category( "Post Processing" )]
[Icon( "zoom_out_map" )]
public sealed class ChromaticAberration : BaseComponent, BaseComponent.ExecuteInEditor
{
	/// <summary>
	/// Enable chromatic aberration
	/// </summary>
	[Property] public float Scale { get; set; } = 1;

	/// <summary>
	/// The pixel offset for each color channel. These values should
	/// be very small as it's in UV space. (0.004 for example)
	/// X = Red
	/// Y = Green
	/// Z = Blue
	/// </summary>
	[Property] public Vector3 Offset { get; set; } = new Vector3( 4f, 6f, 0.0f );


	IDisposable renderHook;

	public override void OnEnabled()
	{
		renderHook?.Dispose();

		var cc = GetComponent<CameraComponent>( false, false );
		renderHook = cc.AddHookBeforeOverlay( "ChromaticAberration", 900, RenderEffect );
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

		attributes.SetCombo( "D_CHROMATIC_ABERRATION", Scale.AlmostEqual( 0.0f ) ? 0 : 1 );
		attributes.Set( "standard.chromaticaberration.scale", Scale );
		attributes.Set( "standard.chromaticaberration.amount", Offset / 1000.0f );

		attributes.Set( "standard.sharpen.strength", 0 );
		attributes.Set( "standard.pixelate.pixelation", 0 );

		Graphics.GrabFrameTexture( "ColorBuffer", attributes );
		Graphics.GrabDepthTexture( "DepthBuffer", attributes );
		Graphics.Blit( Material.Load( "materials/postprocess/standard_pass1.vmat" ), attributes );
	}

}
