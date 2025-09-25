using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a blur effect to the camera.
/// </summary>
[Title( "Blur" )]
[Category( "Post Processing" )]
[Icon( "lens_blur" )]
public sealed class Blur2 : BasePostProcess<Blur2>
{
	[Range( 0, 1 ), Property] public float Size { get; set; } = 1.0f;

	CommandList cl = new CommandList( "Blur" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_blur.shader" );

		float size = ctx.GetWeighted( x => x.Size );

		if ( size <= 0f )
			return;

		cl.Reset();

		attr.Set( "size", size );
		cl.Attributes.GrabFrameTexture( "ColorBuffer", true );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess );
	}
}
