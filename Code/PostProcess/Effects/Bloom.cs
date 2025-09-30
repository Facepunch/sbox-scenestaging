using Sandbox.Rendering;

namespace Sandbox;
using System.Text.Json.Nodes;

/// <summary>
/// Applies a bloom effect to the camera
/// </summary>
[Title( "Bloom" )]
[Category( "Post Processing" )]
[Icon( "exposure" )]
public class Bloom2 : BasePostProcess<Bloom2>
{
	[Property] public SceneCamera.BloomAccessor.BloomMode Mode { get; set; }

	[Range( 0, 10 )]
	[Property] public float Strength { get; set; } = 1.0f;

	[Range( 0, 2 )]
	[Property] public float Threshold { get; set; } = 1.0f;
	[Property, Range( 1.0f, 2.2f )] public float Gamma { get; set; } = 2.2f;
	[Property] public Color Tint { get; set; } = Color.White;

	public enum FilterMode
	{
		Bilinear = 0,
		Biquadratic = 1
	}

	[Property] public FilterMode Filter { get; set; } = FilterMode.Bilinear;

	// Obsolete properties, the above should be enough to keep a natural looking bloom effect
	[Obsolete] public float ThresholdWidth { get; set; }
	[Obsolete] public Curve BloomCurve { get; set; }
	[Obsolete] public Gradient BloomColor { get; set; }

	CommandList command = new CommandList();

	public override void Render()
	{
		if ( Strength == 0.0f )
			return;

		command.Reset();

		// Grab the current frame color once and reuse for compute + composite
		var colorHandle = command.Attributes.GrabFrameTexture( "ColorBuffer", true );

		// Half-resolution target for bloom accumulation
		var bloomRt = command.GetRenderTarget( "BloomTexture", 2, ImageFormat.RGBA16161616F, ImageFormat.None ); // Maybe 1010102F?

		// Bind inputs for compute
		command.Attributes.Set( "Color", colorHandle.ColorTexture );

		// Parameters
		command.Attributes.Set( "Strength", GetWeighted( x=> x.Strength, 0 ) );
		command.Attributes.Set( "Threshold", GetWeighted( x => x.Threshold, 0 ) );
		command.Attributes.Set( "Gamma", GetWeighted( x => x.Gamma, 0 ) );
		command.Attributes.Set( "Tint", GetWeighted( x => x.Tint, Color.White ) );
		command.Attributes.Set( "InvDimensions", new Vector2( 2.0f / Screen.Width, 2.0f / Screen.Height ) );
		command.Attributes.SetCombo( "D_FILTER", Filter );

		// Output target for compute
		command.Attributes.Set( "BloomOut", bloomRt.ColorTexture );

		// Dispatch compute at bloom RT size
		var compute = new ComputeShader( "postprocess_bloom_cs" );
		command.DispatchCompute( compute, bloomRt.Size );

		// Composite: sample the bloom texture in PS and apply selected mode
		command.Attributes.Set( "BloomTexture", bloomRt.ColorTexture );
		command.Attributes.Set( "CompositeMode", (int)Mode );

		command.Blit( Material.FromShader( "postprocess_bloom.shader" ) );

		AddCommandList( command, Stage.BeforePostProcess, 100 );
	}
}
