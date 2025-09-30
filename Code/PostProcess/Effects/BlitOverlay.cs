using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Draw a material over the screen
/// </summary>
[Title( "Blit Overlay" )]
[Category( "Post Processing" )]
[Icon( "grain" )]
public sealed class BlitOverlay : BasePostProcess<BlitOverlay>
{
	[Range( 0, 1 )]
	[Property] public float Blend { get; set; } = 0.1f;

	[Range( 0, 1 )]
	[Property] public BlendMode BlendMode { get; set; } = BlendMode.Normal;

	[Range( 0, 1 )]
	[Property] public Material Material { get; set; }

	public override void Render()
	{
		if ( !Material.IsValid() ) return;

		float blend = GetWeighted( x => x.Blend );
		if ( blend.AlmostEqual( 0.0f ) ) return;

		Attributes.Set( "blend", blend );
		Attributes.SetComboEnum( "D_BLENDMODE", BlendMode );

		Blit( Material, Stage.AfterPostProcess, 100 );
	}

}
