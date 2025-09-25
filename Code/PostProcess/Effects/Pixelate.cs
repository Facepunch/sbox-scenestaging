using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a pixelate effect to the camera
/// </summary>
[Title( "Pixelate" )]
[Category( "Post Processing" )]
[Icon( "apps" )]
public sealed class Pixelate2 : BasePostProcess<Pixelate2>
{
	[Range( 0, 1 )]
	[Property] public float Scale { get; set; } = 0.25f;

	CommandList cl = new CommandList( "ChromaticAberration" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_pixelate.shader" );

		float scale = ctx.GetWeighted( x => x.Scale );
		if ( scale.AlmostEqual( 0.0f ) ) return;

		cl.Reset();

		attr.Set( "scale", scale );

		cl.Attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess );
	}
}
