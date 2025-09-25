using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a film grain effect to the camera
/// </summary>
[Title( "FilmGrain" )]
[Category( "Post Processing" )]
[Icon( "grain" )]
public sealed class FilmGrain2 : BasePostProcess<FilmGrain2>
{
	[Range( 0, 1 )]
	[Property] public float Intensity { get; set; } = 0.1f;

	[Range( 0, 1 )]
	[Property] public float Response { get; set; } = 0.5f;

	CommandList cl = new CommandList( "ChromaticAberration" );
	RenderAttributes attr = new();

	public override void Build( Context ctx )
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_filmgrain.shader" );

		float intensity = ctx.GetWeighted( x => x.Intensity );
		if ( intensity.AlmostEqual( 0.0f ) ) return;

		cl.Reset();

		attr.Set( "intensity", intensity );
		attr.Set( "response", ctx.GetWeighted( x => x.Response, 1 ) );

		cl.Attributes.GrabFrameTexture( "ColorBuffer" );
		cl.Blit( shader, attr );

		ctx.Add( cl, Stage.AfterPostProcess );
	}

}
