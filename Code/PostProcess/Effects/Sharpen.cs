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

	public override void Render()
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_sharpen.shader" );

		float scale = GetWeighted( x => x.Scale );

		if ( scale <= 0f )
			return;

		Attributes.Set( "strength", scale );
		Attributes.Set( "size", GetWeighted( x => x.TexelSize ) );

		Blit( shader, Stage.BeforePostProcess, 100 );
	}
}
