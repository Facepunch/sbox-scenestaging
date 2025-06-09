public sealed class RenderToTextureTest : Component
{
	[Property]
	public bool UseRenderTextureProperty { get; set; }

	Texture texture;

	protected override void OnUpdate()
	{
		texture ??= Texture.CreateRenderTarget( "test", ImageFormat.RGBA16161616F, 512 );

		var cam = GetComponentInChildren<CameraComponent>( true );
		if ( cam is null ) return;

		if ( UseRenderTextureProperty )
		{
			cam.RenderTarget = texture;
		}
		else
		{
			cam.RenderToTexture( texture );
		}

		DebugOverlay.Texture( texture, new Rect( 20, Screen.Height * 0.5f ), Color.White );
	}
}
