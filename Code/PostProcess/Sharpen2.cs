using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a sharpen effect to the camera
/// </summary>
[Title( "Sharpen" )]
[Category( "Post Processing" )]
[Icon( "deblur" )]
public sealed class Sharpen2 : BasePostProcess
{
	[Range( 0, 5 )]
	[Property] public float Scale { get; set; } = 2;

	CommandList cl = new CommandList( "Sharpen" );

	public override void Build( PostProcessContext ctx )
	{
		float scale = ctx.GetFloat<Sharpen2>( x => x.Scale );

		if ( scale <= 0f )
			return;

		cl.Reset();
		var attributes = cl.Attributes;
		attributes.Set( "standard.sharpen.strength", scale );
		attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( Material.Load( "materials/postprocess/standard_pass1.vmat" ) );

		ctx.Add( cl, Stage.AfterPostProcess );
	}
}
