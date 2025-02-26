namespace Sandbox;

[Title( "Bloom (Scene Staging)" )]
[Category( "Post Processing" )]
[Icon( "exposure" )]
public class Bloom2 : Bloom, Component.ExecuteInEditor
{
	IDisposable renderHook;

	protected override void OnEnabled()
	{
		renderHook?.Dispose();

		renderHook = Camera.AddHookAfterTransparent( "Bloom", 1, RenderEffect );
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

		if ( Strength == 0.0f )
			return;

		attributes.Set( "Strength", Strength );

        var material = Material.FromShader( "postprocess_bloom_staging" );

		Graphics.GrabFrameTexture( "ColorBuffer", attributes, withMips: true );
		Graphics.Blit( material, attributes );
	}

}
