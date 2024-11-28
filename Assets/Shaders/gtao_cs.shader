// Why do we need this at all in a compute shader
MODES
{ 
    Default();
    VrForward();
}

CS 
{
    #define XE_GTAO_GENERATE_NORMALS_INPLACE 1 // Generate normals on main pass
    #define XE_GTAO_USE_HALF_FLOAT_PRECISION 0 // dxc has a compiler bug doing dot products with half precision, unsure if much perf gain on desktop
    #define XE_GTAO_FP32_DEPTHS 1
    
    #include "common.fxc"
    #include "postprocess/shared.hlsl"
    #include "postprocess/common.hlsl" 

    #include "common\classes\Depth.hlsl"
    #include "common\classes\Motion.hlsl"
    
    #include "common\thirdparty\XeGTAO.h"
    #include "common\thirdparty\XeGTAO.hlsl"


    //-------------------------------------------------------------------------------------------------------------------

    cbuffer GTAOConstants
    {
        GTAOConstants g_GTAOConsts;
    };

    //-------------------------------------------------------------------------------------------------------------------

    // input output textures for the first pass (XeGTAO_PrefilterDepths16x16)
#if ( D_PASS == 0)
    Texture2DMS<float>           g_srcRawDepth           < Attribute("RawDepth"); > ;           // source depth buffer data (in NDC space in DirectX)
    RWTexture2D<lpfloat>         g_outWorkingDepthMIP0   < Attribute("WorkingDepthMIP0"); > ;   // output viewspace depth MIP (these are views into g_srcWorkingDepth MIP levels)
    RWTexture2D<lpfloat>         g_outWorkingDepthMIP1   < Attribute("WorkingDepthMIP1"); > ;   // output viewspace depth MIP (these are views into g_srcWorkingDepth MIP levels)
    RWTexture2D<lpfloat>         g_outWorkingDepthMIP2   < Attribute("WorkingDepthMIP2"); > ;   // output viewspace depth MIP (these are views into g_srcWorkingDepth MIP levels)
    RWTexture2D<lpfloat>         g_outWorkingDepthMIP3   < Attribute("WorkingDepthMIP3"); > ;   // output viewspace depth MIP (these are views into g_srcWorkingDepth MIP levels)
    RWTexture2D<lpfloat>         g_outWorkingDepthMIP4   < Attribute("WorkingDepthMIP4"); > ;   // output viewspace depth MIP (these are views into g_srcWorkingDepth MIP levels)
    RWTexture2D<uint>            g_outNormalmap          < Attribute("NormalMap"); > ;          // output viewspace normals if generating from depth
#endif

    // input output textures for the second pass (XeGTAO_MainPass)
    Texture2D<lpfloat>           g_srcWorkingDepth       < Attribute("WorkingDepth"); > ;       // viewspace depth with MIPs, output by XeGTAO_PrefilterDepths16x16 and consumed by XeGTAO_MainPass
    RWTexture2D<lpfloat>         g_outWorkingAOTerm      < Attribute("WorkingAOTerm"); > ;      // output AO term (includes bent normals if enabled - packed as R11G11B10 scaled by AO)
    RWTexture2D<lpfloat>         g_outWorkingEdges       < Attribute("WorkingEdges"); > ;       // output depth-based edges used by the denoiser

    // input output textures for the third pass
    Texture2D                    g_srcWorkingAOTerm      < Attribute("WorkingAOTerm"); > ;    // coming from previous pass
    Texture2D<lpfloat>           g_srcWorkingEdges       < Attribute("WorkingEdges"); > ; // coming from previous pass
    RWTexture2D<lpfloat>         g_outAO                 < Attribute("FinalAOTerm"); >;         // final AO term - just 'visibility' or 'visibility + bent normals'
    Texture2D                    g_prevAO                < Attribute("FinalAOTermPrev"); >;

    SamplerState                PointClamp               < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP ); AddressW( CLAMP ); >;
    SamplerState                BilinearClamp            < Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); AddressW( CLAMP ); >;

    Texture2D                   g_tBlueNoise             < Attribute( "BlueNoise" ); >;

    //-------------------------------------------------------------------------------------------------------------------

    enum GTAOPasses
    {
        ViewDepthChain,    // XeGTAO depth filter seems to do average depth, maybe we can do DepthMax + DepthMin from our existing depth chain and average that, removing this pass
        MainPass,
        DenoiseSpatial,
        DenoiseTemporal
    };

    DynamicCombo( D_PASS, 0..3, Sys( All ) );
    DynamicCombo( D_QUALITY, 0..2, Sys( All ) );

    //-------------------------------------------------------------------------------------------------------------------
    float CamFoV < Attribute("cam.fov"); >;

    // These are defined by viewport, CommandList API doesn't expose changing parameters per-viewport so we do it here for now
    GTAOConstants GetConstants()
    {
        GTAOConstants consts = g_GTAOConsts;

        consts.ViewportSize = g_vViewportSize.xy;
		consts.ViewportPixelSize = g_vInvViewportSize.xy;

		float depthLinearizeMul = ( g_flFarPlane * g_flNearPlane ) / ( g_flFarPlane - g_flNearPlane );
		float depthLinearizeAdd = g_flFarPlane / ( g_flFarPlane - g_flNearPlane );

		if ( depthLinearizeMul * depthLinearizeAdd < 0 )
			depthLinearizeAdd = -depthLinearizeAdd;

		consts.DepthUnpackConsts = float2( depthLinearizeMul, depthLinearizeAdd );

		float aspectRatio = (float)g_vViewportSize.x / (float)g_vViewportSize.y;
		float tanHalfFOVY = tan(0.5f * CamFoV * (3.1415f / 180.0f));
		float tanHalfFOVX = tanHalfFOVY * aspectRatio;
		consts.CameraTanHalfFOV = float2(tanHalfFOVX, tanHalfFOVY);

		consts.NDCToViewMul = float2( consts.CameraTanHalfFOV.x * 2.0f, consts.CameraTanHalfFOV.y * -2.0f );
		consts.NDCToViewAdd = float2( consts.CameraTanHalfFOV.x * -1.0f, consts.CameraTanHalfFOV.y * 1.0f );

		consts.NDCToViewMul_x_PixelSize = float2( consts.NDCToViewMul.x * consts.ViewportPixelSize.x, consts.NDCToViewMul.y * consts.ViewportPixelSize.y );

        return consts;
    }

    //-------------------------------------------------------------------------------------------------------------------
    
    [numthreads( 8, 8, 1 )]
    void MainCs( uint2 vDispatchId : SV_DispatchThreadID, uint2 vGroupThreadID : SV_GroupThreadID )
    {
        GTAOConstants sGTAOConsts = GetConstants();

        if (D_PASS == GTAOPasses::ViewDepthChain )
        {
            
            #if( D_PASS == 0 )
                // Write view-space depth MIPs
                XeGTAO_PrefilterDepths16x16(vDispatchId, vGroupThreadID, sGTAOConsts, g_tDepthChain, PointClamp, g_outWorkingDepthMIP0, g_outWorkingDepthMIP1, g_outWorkingDepthMIP2, g_outWorkingDepthMIP3, g_outWorkingDepthMIP4 );
            #endif
        }
        else if (D_PASS == GTAOPasses::MainPass )
        {
            const lpfloat2 localNoise      = g_tBlueNoise[ ( vDispatchId.xy + ( sGTAOConsts.NoiseIndex * float2( 1325, 4125 ) ) ) % 128 ].xy; // Blue noise texture
            const lpfloat3 viewspaceNormal = 0; // Generated on shader until we have DepthNormals() pass

            lpfloat sliceCount;
            lpfloat stepsPerSlice;

            if (D_QUALITY == 0) 
            {
                sliceCount = 3; // Low quality
                stepsPerSlice = 3;
            } 
            else if (D_QUALITY == 1) 
            {
                sliceCount = 4; // Medium quality
                stepsPerSlice = 4;
            }
            else if (D_QUALITY == 2) 
            {
                sliceCount = 7; // High quality
                stepsPerSlice = 7;
            }

            XeGTAO_MainPass
            (
                vDispatchId,
                sliceCount,
                stepsPerSlice,
                localNoise,
                0,
                sGTAOConsts,
                g_srcWorkingDepth,
                PointClamp,
                g_outWorkingAOTerm,
                g_outWorkingEdges
            );
        }
        else if (D_PASS == GTAOPasses::DenoiseSpatial )
        {
            g_outAO[vDispatchId] =  XeGTAO_Denoise
            (
                vDispatchId,        // const uint2 pixCoordBase
                sGTAOConsts,       // const GTAOConstants consts
                g_srcWorkingAOTerm, // Texture2D<uint> sourceAOTerm
                g_srcWorkingEdges,  // Texture2D<lpfloat> sourceEdges
                PointClamp         // SamplerState texSampler
            );
        }
        else if( D_PASS == GTAOPasses::DenoiseTemporal )
        {
            g_outAO[vDispatchId] = Motion::TemporalFilter( vDispatchId.xy, g_srcWorkingAOTerm, g_prevAO, g_GTAOConsts.TAABlendAmount ).r;
        }
    }
}