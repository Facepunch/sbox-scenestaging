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

	public override void Render()
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_pixelate.shader" );

		float scale = GetWeighted( x => x.Scale );
		if ( scale.AlmostEqual( 0.0f ) ) return;

		Attributes.Set( "scale", scale );

		Blit( shader, Stage.AfterPostProcess, 10000 );
	}
}
