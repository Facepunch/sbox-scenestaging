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

	public override void Render()
	{
		var shader = Material.FromShader( "shaders/postprocess/pp_filmgrain.shader" );

		float intensity = GetWeighted( x => x.Intensity );
		if ( intensity.AlmostEqual( 0.0f ) ) return;

		float response = GetWeighted( x => x.Response, 1 );

		Attributes.Set( "intensity", intensity );
		Attributes.Set( "response", GetWeighted( x => x.Response, 1 ) );

		Blit( shader, Stage.AfterPostProcess, 100 );
	}

}
