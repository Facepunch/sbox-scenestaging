using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a chromatic aberration effect to the camera
/// </summary>
[Title( "Chromatic Aberration" )]
[Category( "Post Processing" )]
[Icon( "zoom_out_map" )]
public sealed class ChromaticAberration2 : BasePostProcess<ChromaticAberration2>
{
	/// <summary>
	/// Enable chromatic aberration
	/// </summary>
	[Property, Range( 0, 1 )] public float Scale { get; set; } = 0.33f;

	/// <summary>
	/// The pixel offset for each color channel. These values should
	/// be very small as it's in UV space. (0.004 for example)
	/// X = Red
	/// Y = Green
	/// Z = Blue
	/// </summary>
	[Property] public Vector3 Offset { get; set; } = new Vector3( 4f, 6f, 0.0f );


	CommandList cl = new CommandList( "ChromaticAberration" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_chromaticaberration.shader" );

		float scale = ctx.GetWeighted( x => x.Scale );
		Vector3 offset = ctx.GetWeighted( x => x.Offset );

		if ( scale <= 0f )
			return;

		cl.Reset();
		var attributes = cl.Attributes;

		attr.Set( "scale", scale );
		attr.Set( "amount", offset / 1000.0f );

		cl.Attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess );
	}
}
