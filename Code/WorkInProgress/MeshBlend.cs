using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Camera post-process that smoothly blends seams between objects with <see cref="MeshBlendTarget"/>.
/// Pipeline: mask rasterization → edge detection → JFA expansion → UV-mirror resolve.
/// </summary>
[Title( "Mesh Blend" )]
[Category( "Post Processing" )]
[Icon( "blur_on" )]
public sealed class MeshBlend : BasePostProcess<MeshBlend>
{
	/// <summary>
	/// Max JFA step size. Must match ScreenBlendRadius in the resolve shader.
	/// Determines number of JFA passes (log2 steps).
	/// </summary>
	private const int MaxJfaDistance = 64;

	CommandList commands = new( "Mesh Blend" );

	private static readonly Material MaskShader = Material.FromShader( "meshblend_mask.shader" );
	private static readonly ComputeShader PrepareCs = new( "meshblend_prepare_cs" );
	private static readonly ComputeShader ExpandCs = new( "meshblend_expand_cs" );
	private static readonly Material ResolveShader = Material.FromShader( "meshblend_resolve.shader" );

	public override void Render()
	{
		var blendTargets = Scene.GetAll<MeshBlendTarget>().ToList();
		if ( blendTargets.Count == 0 )
			return;

		commands.Reset();

		var colorHandle = commands.Attributes.GrabFrameTexture( "ColorBuffer", false );

		// Mask RT: R = normalized region ID, G = blend falloff. D24S8 for self-occlusion.
		var maskRt = commands.GetRenderTarget( "MeshBlendMask", 1, ImageFormat.RG1616F, ImageFormat.D24S8, msaa: MultisampleAmount.MultisampleNone );

		// Edge maps for JFA ping-pong (uint2 pixel coordinates)
		var edgeMapA = commands.GetRenderTarget( "MeshBlendEdgeA", 1, ImageFormat.RG3232, ImageFormat.None );
		var edgeMapB = commands.GetRenderTarget( "MeshBlendEdgeB", 1, ImageFormat.RG3232, ImageFormat.None );

		// Pass 1: Rasterize blend targets to mask RT
		commands.SetRenderTarget( maskRt );
		commands.Clear( Color.Transparent, true, true, false );

		foreach ( var target in blendTargets )
		{
			DrawMaskPass( commands, target );
		}

		commands.ClearRenderTarget();
		commands.ResourceBarrierTransition( maskRt, ResourceState.PixelShaderResource );

		// Pass 2: Edge detection via LDS-cached neighbor comparison
		commands.Attributes.Set( "InputMask", maskRt.ColorTexture );
		commands.Attributes.Set( "OutputEdgeMap", edgeMapA.ColorTexture );
		commands.DispatchCompute( PrepareCs, maskRt.Size );
		commands.ResourceBarrierTransition( edgeMapA, ResourceState.PixelShaderResource );

		// Pass 3: JFA expansion (halving step size each pass)
		bool useAAsInput = true;

		for ( int step = MaxJfaDistance; step >= 1; step /= 2 )
		{
			var currentIn = useAAsInput ? edgeMapA : edgeMapB;
			var currentOut = useAAsInput ? edgeMapB : edgeMapA;

			commands.Attributes.Set( "InputEdgeMap", currentIn.ColorTexture );
			commands.Attributes.Set( "Mask", maskRt.ColorTexture );
			commands.Attributes.Set( "OutputEdgeMap", currentOut.ColorTexture );
			commands.Attributes.Set( "StepSize", step );
			commands.DispatchCompute( ExpandCs, maskRt.Size );

			commands.ResourceBarrierTransition( currentOut, ResourceState.PixelShaderResource );
			useAAsInput = !useAAsInput;
		}

		var finalEdgeMap = useAAsInput ? edgeMapA : edgeMapB;

		// Pass 4: Resolve blend via UV mirroring across seams
		commands.Attributes.Set( "ColorBuffer", colorHandle.ColorTexture );
		commands.Attributes.Set( "MaskTexture", maskRt.ColorTexture );
		commands.Attributes.Set( "EdgeMap", finalEdgeMap.ColorTexture );
		commands.Blit( ResolveShader );

		InsertCommandList( commands, Stage.BeforePostProcess, 50, "Mesh Blend" );
	}

	private void DrawMaskPass( CommandList commands, MeshBlendTarget target )
	{
		var attributes = commands.Attributes;
		attributes.Set( "RegionId", target.RegionId );
		attributes.Set( "BlendFalloff", target.BlendFalloff );

		foreach ( var renderer in target.GetBlendTargets() )
		{
			commands.DrawRenderer( renderer, new RendererSetup { Material = MaskShader } );
		}
	}
}
