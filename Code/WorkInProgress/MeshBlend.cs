using Sandbox.Rendering;

namespace Sandbox;

/// <summary>
/// Which intermediate buffer to visualize for debugging.
/// </summary>
public enum MeshBlendDebugView
{
	/// <summary>No debug overlay — normal resolve output.</summary>
	None,
	/// <summary>Pass 1: Region mask (R = region ID, G = blend falloff).</summary>
	Mask,
	/// <summary>Pass 2: Edge detection output.</summary>
	Edges,
	/// <summary>Pass 3: JFA expansion result.</summary>
	JFA,
	/// <summary>Pass 4: Final resolve (same as None but explicit).</summary>
	Resolve
}

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
	/// Visualize an intermediate pass for debugging. Leave as None for normal rendering.
	/// </summary>
	[Property, Title( "Debug View" )]
	public MeshBlendDebugView DebugView { get; set; } = MeshBlendDebugView.None;
	/// <summary>
	/// Max JFA step size. Only needs to cover half of ScreenBlendRadius because
	/// the resolve weight hits zero at 0.5 * ScreenBlendRadius. Fewer steps = fewer dispatches.
	/// </summary>
	private const int MaxJfaDistance = 32;

	CommandList commands = new( "Mesh Blend" );

	private static readonly Material MaskShader = Material.FromShader( "meshblend_mask.shader" );
	private static readonly ComputeShader PrepareCs = new( "meshblend_prepare_cs" );
	private static readonly ComputeShader ExpandCs = new( "meshblend_expand_cs" );
	private static readonly Material ResolveShader = Material.FromShader( "meshblend_resolve.shader" );
	private static readonly Material DebugShader = Material.FromShader( "meshblend_debug.shader" );

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

		if ( DebugView != MeshBlendDebugView.None && DebugView != MeshBlendDebugView.Resolve )
		{
			// Debug visualization of intermediate buffers
			commands.Attributes.Set( "DebugMode", (int)DebugView );
			commands.Blit( DebugShader );
		}
		else
		{
			commands.Blit( ResolveShader );
		}

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
