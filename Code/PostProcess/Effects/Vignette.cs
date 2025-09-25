using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a vignette to the camera
/// </summary>
[Title( "Vignette" )]
[Category( "Post Processing" )]
[Icon( "vignette" )]
public sealed class Vignette2 : BasePostProcess<Vignette2>
{
	/// <summary>
	/// The color of the vignette or the "border"
	/// </summary>
	[Property] public Color Color { get; set; } = Color.Black;

	/// <summary>
	/// How strong the vignette is. This is a value between 0 -> 1
	/// </summary>
	[Property, Range( 0, 1 )] public float Intensity { get; set; } = 1.0f;

	/// <summary>
	/// How much fall off or how blurry the vignette is
	/// </summary>
	[Property, Range( 0, 1 )] public float Smoothness { get; set; } = 1.0f;

	/// <summary>
	/// How circular or round the vignette is
	/// </summary>
	[Property, Range( 0, 1 )] public float Roundness { get; set; } = 1.0f;

	/// <summary>
	/// The center of the vignette in relation to UV space. This means
	/// a value of {0.5, 0.5} is the center of the screen
	/// </summary>
	[Property] public Vector2 Center { get; set; } = new Vector2( 0.5f, 0.5f );

	CommandList cl = new CommandList( "ChromaticAberration" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_vignette.shader" );

		float intensity = ctx.GetWeighted( x => x.Intensity );
		if ( intensity.AlmostEqual( 0.0f ) ) return;

		var color = ctx.GetWeighted( x => x.Color );
		if ( color.a.AlmostEqual( 0.0f ) ) return;

		cl.Reset();

		attr.Set( "color", color );
		attr.Set( "intensity", intensity );
		attr.Set( "smoothness", ctx.GetWeighted( x => x.Smoothness ) );
		attr.Set( "roundness", ctx.GetWeighted( x => x.Roundness ) );
		attr.Set( "center", ctx.GetWeighted( x => x.Center ) );

		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess );
	}
}
