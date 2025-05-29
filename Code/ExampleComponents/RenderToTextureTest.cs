public sealed class RenderToTextureTest : Component
{
	Texture texture;

	protected override void OnUpdate()
	{
		texture ??= Texture.CreateRenderTarget( "test", ImageFormat.RGBA16161616F, 512 );
		texture.Clear( Color.Red );

		var cam = GetComponentInChildren<CameraComponent>( true );
		if ( cam is null ) return;

		cam.RenderToTexture( texture );

		DebugOverlay.Texture( texture, new Rect( 20, Screen.Height * 0.5f ), Color.White );
	}
}
