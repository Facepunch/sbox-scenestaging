using Sandbox.Rendering;

namespace Sandbox;

[Title( "Dynamic Reflections (SSR)" )]
[Category( "Post Processing" )]
[Icon( "local_mall" )]
[Hide]
public class DynamicReflections : PostProcess, Component.ExecuteInEditor
{
    Rendering.CommandList commands;
    Rendering.CommandList _getLastFrameColorCommand;
    int Frame;

    Texture BlueNoise { get; set; } = Texture.Load( "textures/dev/blue_noise_256.vtex" );

    /// <summary>
    /// Until which roughness value the effect should be applied
    /// </summary>
    [Property, Range( 0.0f, 0.9f )] float RoughnessCutoff { get; set; } = 0.5f;

    /// <summary>
    /// Quality of the effect ( Full res, Half res, Quarter res )
    /// Default half res
    /// </summary>
    [Property] int Quality = 1;

    /// <summary>
    /// Number of samples per pixel
    /// </summary>
    [Property, Hide] int SamplesPerPixel = 1;

    enum Passes
    {
        ClassifyTiles,
        Intersect,
        DenoiseReproject,
        DenoisePrefilter,
        DenoiseResolveTemporal
    }

    protected override void OnEnabled()
    {
        commands = new Rendering.CommandList( "Dynamic Reflections" );
        OnDirty();

        Camera.AddCommandList( commands, Rendering.Stage.AfterDepthPrepass );
    }

    protected override void OnDisabled()
    {
        Camera.RemoveCommandList( commands );
        commands = null;
        Frame = 0;
    }

    protected override void OnUpdate()
    {
        Rebuild();
    }

    void Rebuild()
    {
        if ( commands is null )
            return;

        commands.Reset();
        
		bool pingPong = (Frame++ % 2) == 0;
        int downsampleRatio = (int)Math.Pow( 2, Quality );

        commands.Set( "BlueNoiseIndex", BlueNoise.Index );

        commands.GrabFrameTexture( "PrevFrameTexture" );

        var PreviousFrameColorRT = commands.GetRenderTarget( "PrevFrameTexture", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );
        var PreviousGBuffer	     = commands.GetRenderTarget( "PrevGBuffer",  ImageFormat.RGBA8888, sizeFactor: downsampleRatio );

        var Radiance0 = commands.GetRenderTarget( $"Radiance{pingPong}", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );
        var Radiance1 = commands.GetRenderTarget( $"Radiance{!pingPong}", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );

        var Variance0 = commands.GetRenderTarget( $"Variance{pingPong}", ImageFormat.R16F, sizeFactor: downsampleRatio );
        var Variance1 = commands.GetRenderTarget( $"Variance{!pingPong}", ImageFormat.R16F, sizeFactor: downsampleRatio );

        var SampleCount0 = commands.GetRenderTarget( $"Sample Count{pingPong}", ImageFormat.R16F, sizeFactor: downsampleRatio );
        var SampleCount1 = commands.GetRenderTarget( $"Sample Count{!pingPong}", ImageFormat.R16F, sizeFactor: downsampleRatio );

        var AverageRadiance0 = commands.GetRenderTarget( $"Average Radiance{pingPong}", ImageFormat.RGBA16161616F, sizeFactor: 8 * downsampleRatio );
        var AverageRadiance1 = commands.GetRenderTarget( $"Average Radiance{!pingPong}", ImageFormat.RGBA16161616F, sizeFactor: 8 * downsampleRatio );

        var ReprojectedRadiance	= commands.GetRenderTarget( "Reprojected Radiance", ImageFormat.RGBA16161616F, sizeFactor: downsampleRatio );

        ComputeShader reflectionsCs = new ComputeShader("dynamic_reflections_cs");

        foreach( var pass in Enum.GetValues( typeof( Passes ) ) )
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
                break;

            case Passes.DenoiseReproject:
                commands.Set( "SampleCountIntersection", SamplesPerPixel );
                commands.Set( "AverageRadianceHistory", AverageRadiance1.ColorTexture );
                commands.Set( "VarianceHistory", Variance1.ColorTexture ); 
                commands.Set( "SampleCountHistory", SampleCount1.ColorTexture );

                commands.Set( "OutReprojectedRadiance", ReprojectedRadiance.ColorTexture );
                commands.Set( "OutAverageRadiance", AverageRadiance0.ColorTexture );
                commands.Set( "OutVariance", Variance0.ColorTexture );
                commands.Set( "OutSampleCount", SampleCount0.ColorTexture );
                break;

            case Passes.DenoisePrefilter:
                commands.Set( "Radiance", Radiance0.ColorTexture );
                commands.Set( "Variance", Variance0.ColorTexture );
                commands.Set( "SampleCountHistory", SampleCount0.ColorTexture );

                commands.Set( "OutRadiance", Radiance1.ColorTexture );
                commands.Set( "OutVariance", Variance1.ColorTexture );
                commands.Set( "OutSampleCount", SampleCount1.ColorTexture );
                break;

            case Passes.DenoiseResolveTemporal:
                commands.Set( "Radiance", Radiance1.ColorTexture );
                commands.Set( "ReprojectedRadiance", ReprojectedRadiance.ColorTexture );
                commands.Set( "Variance", Variance1.ColorTexture );
                commands.Set( "SampleCount", SampleCount1.ColorTexture );

                commands.Set( "OutRadiance", Radiance0.ColorTexture );
                commands.Set( "OutVariance", Variance0.ColorTexture );
                commands.Set( "OutSampleCount", SampleCount0.ColorTexture );
                break;
            }

            // Common settings for all passes
            commands.Set( "PreviousGBuffer", PreviousGBuffer.ColorTexture );
            commands.Set( "PreviousFrameColor", PreviousFrameColorRT.ColorTexture );
            commands.Set( "AverageRadiance", AverageRadiance0.ColorTexture );
            commands.Set( "AverageRadianceHistory", AverageRadiance1.ColorTexture );

            // Set the pass
            commands.SetCombo( "D_PASS", (int)pass );
            commands.DispatchCompute( reflectionsCs, ReprojectedRadiance.Size );
        }

        // Final SSR color to be used by shaders
        commands.SetGlobal( "ReflectionColorIndex", Radiance0.ColorIndex );
    }
}
