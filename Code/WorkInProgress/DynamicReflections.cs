namespace Sandbox;

[Title( "Dynamic Reflections (SSR)" )]
[Category( "Post Processing" )]
[Icon( "local_mall" )]
public class DynamicReflections : PostProcess, Component.ExecuteInEditor
{
	int Frame;

	Texture BlueNoise { get; set; } = Texture.Load( FileSystem.Mounted, "textures/dev/blue_noise_256.vtex" );

	/// <summary>
	/// Until which roughness value the effect should be applied
	/// </summary>
	[Property, Range( 0.0f, 0.9f )] float RoughnessCutoff { get; set; } = 0.5f;

	[Property] bool Denoise = true;

	enum Passes
	{
		//ClassifyTiles,
		Intersect,
		DenoiseReproject,
		DenoisePrefilter,
		DenoiseResolveTemporal
	}
	
	Rendering.CommandList Commands;
	IDisposable FetchLastFrameHook;
	RenderTarget LastFrameRT { get; set; } = null;

	protected override void OnEnabled()
	{
		Commands = new Rendering.CommandList( "Dynamic Reflections" );

		OnDirty();

		Camera.AddCommandList( Commands, Rendering.Stage.AfterDepthPrepass, int.MaxValue );
		FetchLastFrameHook = Camera.AddHookAfterTransparent( "FetchLastFrameColor", 0, FetchLastFrameColor );

	}

	protected override void OnDisabled()
	{
		Camera.RemoveCommandList( Commands );
		FetchLastFrameHook?.Dispose();

		Commands = null;
		FetchLastFrameHook = null;

		Frame = 0;
	}

	protected override void OnUpdate()
	{
		Rebuild();
	}

	void Rebuild()
	{
		if ( Commands is null )
			return;

		Commands.Reset();

		bool pingPong = (Frame++ % 2) == 0;
		int downsampleRatio = 1;

		Commands.Set( "BlueNoiseIndex", BlueNoise.Index );

		var Radiance0 = Commands.GetRenderTarget( "Radiance0", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );
		var Radiance1 = Commands.GetRenderTarget( "Radiance1", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );

		var Variance0 = Commands.GetRenderTarget( "Variance0", ImageFormat.R16F, sizeFactor: downsampleRatio );
		var Variance1 = Commands.GetRenderTarget( "Variance1", ImageFormat.R16F, sizeFactor: downsampleRatio );

		var SampleCount0 = Commands.GetRenderTarget( "Sample Count0", ImageFormat.R16F, sizeFactor: downsampleRatio );
		var SampleCount1 = Commands.GetRenderTarget( "Sample Count1", ImageFormat.R16F, sizeFactor: downsampleRatio );

		var AverageRadiance0 = Commands.GetRenderTarget( "Average Radiance0", ImageFormat.RGBA8888, sizeFactor: 8 * downsampleRatio );
		var AverageRadiance1 = Commands.GetRenderTarget( "Average Radiance1", ImageFormat.RGBA8888, sizeFactor: 8 * downsampleRatio );

		var ReprojectedRadiance = Commands.GetRenderTarget( "Reprojected Radiance", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );

		var RayLength = Commands.GetRenderTarget( "Ray Length", ImageFormat.R16F, sizeFactor: downsampleRatio );
		var DepthHistory = Commands.GetRenderTarget( "Previous Depth", ImageFormat.R16F, sizeFactor: downsampleRatio );
		var GBufferHistory = Commands.GetRenderTarget( "Previous GBuffer", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );

		ComputeShader reflectionsCs = new ComputeShader( "dynamic_reflections_cs" );

		// Common settings for all passes
		Commands.Set( "GBufferHistory", GBufferHistory.ColorTexture );
		Commands.Set( "PreviousFrameColor", LastFrameRT.ColorTarget );
		Commands.Set( "DepthHistory", DepthHistory.ColorTexture );

		Commands.Set( "RayLength", RayLength.ColorTexture );
		Commands.Set( "RoughnessCutoff", RoughnessCutoff );

		foreach ( var pass in Enum.GetValues( typeof( Passes ) ) )
		{
			switch ( pass )
			{
				// I'd like to use the ray dispatches from GPU Buffers , which would be faster and higher quality
				// but this is hard in the command list api without having per-viewport configuration
				// right now it's a direct reimplementation of C++ version but without Reflection MODE
				// case Passes.ClassifyTiles:
				//    {
				//        break;
				//    }
				case Passes.Intersect:
					Commands.Set( "OutRadiance", pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					break;

				case Passes.DenoiseReproject:
					Commands.Set( "Radiance", pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					Commands.Set( "RadianceHistory", !pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );

					Commands.Set( "AverageRadianceHistory", !pingPong ? AverageRadiance0.ColorTexture : AverageRadiance1.ColorTexture );
					Commands.Set( "VarianceHistory", !pingPong ? Variance0.ColorTexture : Variance1.ColorTexture );
					Commands.Set( "SampleCountHistory", !pingPong ? SampleCount0.ColorTexture : SampleCount1.ColorTexture );

					Commands.Set( "OutReprojectedRadiance", ReprojectedRadiance.ColorTexture );
					Commands.Set( "OutAverageRadiance", pingPong ? AverageRadiance0.ColorTexture : AverageRadiance1.ColorTexture );
					Commands.Set( "OutVariance", pingPong ? Variance0.ColorTexture : Variance1.ColorTexture );
					Commands.Set( "OutSampleCount", pingPong ? SampleCount0.ColorTexture : SampleCount1.ColorTexture );
					break;

				case Passes.DenoisePrefilter:
					Commands.Set( "Radiance", pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					Commands.Set( "RadianceHistory", !pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					Commands.Set( "AverageRadiance", pingPong ? AverageRadiance0.ColorTexture : AverageRadiance1.ColorTexture );
					Commands.Set( "Variance", pingPong ? Variance0.ColorTexture : Variance1.ColorTexture );
					Commands.Set( "SampleCountHistory", pingPong ? SampleCount0.ColorTexture : SampleCount1.ColorTexture );

					Commands.Set( "OutRadiance", !pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					Commands.Set( "OutVariance", !pingPong ? Variance0.ColorTexture : Variance1.ColorTexture );
					Commands.Set( "OutSampleCount", !pingPong ? SampleCount0.ColorTexture : SampleCount1.ColorTexture );
					break;

				case Passes.DenoiseResolveTemporal:
					Commands.Set( "AverageRadiance", pingPong ? AverageRadiance0.ColorTexture : AverageRadiance1.ColorTexture );
					Commands.Set( "Radiance", !pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					Commands.Set( "ReprojectedRadiance", ReprojectedRadiance.ColorTexture );
					Commands.Set( "Variance", !pingPong ? Variance0.ColorTexture : Variance1.ColorTexture );
					Commands.Set( "SampleCount", !pingPong ? SampleCount0.ColorTexture : SampleCount1.ColorTexture );

					Commands.Set( "OutRadiance", pingPong ? Radiance0.ColorTexture : Radiance1.ColorTexture );
					Commands.Set( "OutVariance", pingPong ? Variance0.ColorTexture : Variance1.ColorTexture );
					Commands.Set( "OutSampleCount", pingPong ? SampleCount0.ColorTexture : SampleCount1.ColorTexture );

					Commands.Set( "GBufferHistoryRW", GBufferHistory.ColorTexture );
					Commands.Set( "DepthHistoryRW", DepthHistory.ColorTexture );
					break;
			}


			// Set the pass
			Commands.SetCombo( "D_PASS", (int)pass );
			Commands.DispatchCompute( reflectionsCs, ReprojectedRadiance.Size );

			if ( !Denoise )
				break;
		}

		// Final SSR color to be used by shaders
		Commands.SetGlobal( "ReflectionColorIndex", pingPong ? Radiance0.ColorIndex : Radiance1.ColorIndex );

	}

	// This should be called after opaque pass
	protected void FetchLastFrameColor( SceneCamera camera )
	{
		LastFrameRT = Graphics.GrabFrameTexture( "LastFrameColor" );
	}

}
