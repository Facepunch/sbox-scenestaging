using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a sharpen effect to the camera
/// </summary>
[Title( "Sharpen" )]
[Category( "Post Processing" )]
[Icon( "deblur" )]
public sealed class Sharpen2 : BasePostProcess<Sharpen2>
{
	[Range( 0, 5 )]
	[Property] public float Scale { get; set; } = 2;

	[Range( 0, 5 )]
	[Property] public float TexelSize { get; set; } = 1;

	CommandList cl = new CommandList( "Sharpen" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_sharpen.shader" );

		float scale = ctx.GetWeighted( x => x.Scale );

		if ( scale <= 0f )
			return;

		cl.Reset();

		attr.Set( "strength", scale );
		attr.Set( "size", ctx.GetWeighted( x => x.TexelSize ) );
		cl.Attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess );
	}
}
