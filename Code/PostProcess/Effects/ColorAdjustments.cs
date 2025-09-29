using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies color adjustments to the camera.
/// </summary>
[Title( "Color Adjustments" )]
[Category( "Post Processing" )]
[Icon( "palette" )]
public sealed class ColorAdjustments2 : BasePostProcess<ColorAdjustments2>
{
	[Range( 0, 1 ), Property] public float Blend { get; set; } = 1.0f;
	[Range( 0, 2 ), Property] public float Saturation { get; set; } = 1.0f;
	[Range( 0, 360 ), Property] public float HueRotate { get; set; } = 0.0f;
	[Range( 0, 2 ), Property] public float Brightness { get; set; } = 1.0f;
	[Range( 0, 2 ), Property] public float Contrast { get; set; } = 1.0f;

	CommandList cl = new CommandList( "ColorAdjustments" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_color.shader" );

		cl.Reset();

		attr.Set( "blend", ctx.GetWeighted( x => x.Blend, 0 ) );
		attr.Set( "saturate", ctx.GetWeighted( x => x.Saturation, 1 ) );
		attr.Set( "hue_rotate", ctx.GetWeighted( x => x.HueRotate, 0, true ) );
		attr.Set( "brightness", ctx.GetWeighted( x => x.Brightness, 1 ) );
		attr.Set( "contrast", ctx.GetWeighted( x => x.Contrast, 1 ) );

		cl.Attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess, 100 );
	}

}
