using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Applies a depth of field effect to the camera
/// </summary>
[Title( "Depth Of Field" )]
[Category( "Post Processing" )]
[Icon( "center_focus_strong" )]
public sealed class DepthOfField2 : BasePostProcess<DepthOfField2>
{
	/// <summary>
	/// How blurry to make stuff that isn't in focus.
	/// </summary>
	[Range( 0, 100 )]
	[Property, Group( "Focus" ), Icon( "blur_circular" )]
	public float BlurSize { get; set; } = 30.0f;

	/// <summary>
	/// How far away from the camera to focus in world units.
	/// </summary>
	[Range( 1.0f, 1000 )]
	[Property, Group( "Focus" ), Icon( "horizontal_distribute" )]
	public float FocalDistance { get; set; } = 200.0f;

	/// <summary>
	/// This modulates how far is the blur to the image.
	/// </summary>
	[Property, Range( 0.0f, 1000.0f ), Group( "Focus" ), Icon( "blur_linear" )]
	public float FocusRange { get; set; } = 500f;

	public enum Quality
	{
		[Icon( "center_focus_strong" )]
		High = 1,

		[Icon( "filter_center_focus" )]
		Medium = 2,

		[Icon( "center_focus_weak" )]
		Low = 3,
	}


	[Property, Group( "Properties" )]
	public Quality QualityLevel { get; set; } = Quality.High;

	/// <summary>
	/// Should we blur what's ahead the focal point towards us?
	/// </summary>
	[Property, Group( "Properties" ), Icon( "flip_to_back" ), Hide]
	public bool FrontBlur { get; set; } = false;

	/// <summary>
	/// Should we blur what's behind the focal point?
	/// </summary>
	[Property, Group( "Properties" ), Icon( "flip_to_front" ), Hide]
	public bool BackBlur { get; set; } = true;


	CommandList command = new CommandList( "Depth Of Field" );

	public override void Build( Context ctx )
	{
		if ( !BackBlur && !FrontBlur )
			return;

		float blurSize = ctx.GetWeighted( x => x.BlurSize, 0.0f );
		if ( blurSize < 0.5f ) return;

		float focalDistance = ctx.GetWeighted( x => x.FocalDistance, 200.0f );
		float focalLength = ctx.GetWeighted( x => x.FocusRange, 500.0f );

		command.Reset();

		var compute = new ComputeShader( "postprocess_standard_dof_cs" );

		var downsample = (int)QualityLevel;
		var downsampleExp = (int)MathF.Pow( 2, downsample );  // SizeFactor in RenderTarget.GetTemporary uses division rather than base 2

		command.Attributes.SetValue( "Color", RenderValue.ColorTarget );
		command.Attributes.SetValue( "Depth", RenderValue.DepthTarget );
		command.Attributes.SetValue( "D_MSAA", RenderValue.MsaaCombo );

		var Vertical = command.GetRenderTarget( "Vertical", downsampleExp, ImageFormat.RGBA16161616F, ImageFormat.None );
		var Diagonal = command.GetRenderTarget( "Diagonal", downsampleExp, ImageFormat.RGBA16161616F, ImageFormat.None );
		var Final = command.GetRenderTarget( "Final", downsampleExp, ImageFormat.RGBA16161616F, ImageFormat.None );
		
		command.Attributes.Set( "InvDimensions", Vertical.Size, true );


		command.Attributes.Set( "Radius", (int)(blurSize / downsampleExp) );

		command.Attributes.Set( "Downsample", downsample );
		command.Attributes.Set( "DownsampleExp", downsampleExp );

		command.Attributes.Set( "VerticalSRV", Vertical.ColorTexture );
		command.Attributes.Set( "DiagonalSRV", Diagonal.ColorTexture );
		command.Attributes.Set( "FinalSRV", Final.ColorTexture );

		command.Attributes.Set( "Vertical", Vertical.ColorTexture );
		command.Attributes.Set( "Diagonal", Diagonal.ColorTexture );
		command.Attributes.Set( "Final", Final.ColorTexture );

		command.Attributes.Set( "FocusGap", 0 );

		foreach ( DoFTypes type in Enum.GetValues( typeof( DoFTypes ) ) )
		{
			if ( !BackBlur && type == DoFTypes.BackBlur )
				continue;

			if ( !FrontBlur && type == DoFTypes.FrontBlur )
				continue;


			command.Attributes.Set( "FocusPlane", focalDistance.Clamp( 0, 5000 ) );
			command.Attributes.Set( "FocalLength", focalLength );
			command.Attributes.SetCombo( "D_DOF_TYPE", type );

			command.Attributes.SetCombo( "D_PASS", BlurPasses.CircleOfConfusion );
			command.DispatchCompute( compute, Vertical.Size );

			command.Attributes.SetCombo( "D_PASS", BlurPasses.Blur );
			command.DispatchCompute( compute, Vertical.Size );

			command.Attributes.SetCombo( "D_PASS", BlurPasses.RhomboidBlur );
			command.DispatchCompute( compute, Vertical.Size );

			command.Attributes.SetCombo( "D_DOF_TYPE", type );
			command.Attributes.SetCombo( "D_PASS", 0 );

			var composite = Material.FromShader( "postprocess_standard_dof.shader" );
			command.Blit( composite );
		}

		ctx.Add( command, Stage.AfterTransparent, 100 );
	}

	private enum BlurPasses
	{
		CircleOfConfusion,
		Blur,
		RhomboidBlur,
	};

	private enum DoFTypes
	{
		BackBlur,
		FrontBlur,
	};
}
