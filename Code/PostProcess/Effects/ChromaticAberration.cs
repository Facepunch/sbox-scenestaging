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

	public override void Render()
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_chromaticaberration.shader" );

		float scale = GetWeighted( x => x.Scale );
		Vector3 offset = GetWeighted( x => x.Offset );

		if ( scale <= 0f )
			return;

		Attributes.Set( "scale", scale );
		Attributes.Set( "amount", offset / 1000.0f );

		Blit( shader, Stage.AfterPostProcess, 1000 );
	}
}
