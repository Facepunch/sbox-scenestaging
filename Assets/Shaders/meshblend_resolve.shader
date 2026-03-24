// Mesh Blend Resolve Shader
// Blends seam edges by UV-mirroring color across region boundaries

HEADER
{
    Description = "Mesh Blend Resolve";
}

MODES
{
    Default();
    Forward();
}

FEATURES
{
}

COMMON
{
    #include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
    float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
    float2 vTexCoord : TEXCOORD0;

    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 vPositionPs : SV_Position;
    #endif

    #if ( PROGRAM == VFX_PROGRAM_PS )
        float4 vPositionSs : SV_Position;
    #endif
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        o.vPositionPs = float4( i.vPositionOs.xy, 0.0f, 1.0f );
        o.vTexCoord = i.vTexCoord;
        return o;
    }
}

PS
{
    #include "postprocess/common.hlsl"
    #include "common.fxc"
    #include "common/classes/Depth.hlsl"

    // Color buffer to blend
    Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( false ); >;
    SamplerState g_sLinearClamp < Filter( MIN_MAG_MIP_LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;
    Texture2D<float2> g_tMask < Attribute( "MaskTexture" ); >;
    Texture2D<float> g_tMaskDepth < Attribute( "MaskDepth" ); >;
    Texture2D<uint2> g_tEdgeMap < Attribute( "EdgeMap" ); >;

    static const uint INVALID_EDGE_VALUE = 0xFFFFFFFF;
    static const float ScreenBlendRadius = 32.0;

    // Gather sample positions: (0,1), (1,1), (1,0), (0,0)
    static const float2 gatherOffsets[4] = {
        float2( 0, 1 ),  // index 0
        float2( 1, 1 ),  // index 1
        float2( 1, 0 ),  // index 2
        float2( 0, 0 )   // index 3
    };

    float4 MainPs( PixelInput input ) : SV_Target0
    {
        uint2 pixelCoord = uint2( input.vPositionSs.xy );

        // Early-out: no mask geometry at this pixel (depth buffer wasn't written)
        float maskDepth = g_tMaskDepth.Load( int3( pixelCoord, 0 ) ).r;
        if ( maskDepth <= 0.0 )
            return float4( g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb, 1 );

        uint2 nearestEdge = g_tEdgeMap.Load( int3( pixelCoord, 0 ) );

        if ( any( nearestEdge == INVALID_EDGE_VALUE ) )
            return float4( g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb, 1 );

        // Single load per texel — avoid redundant mask fetches
        float2 currentMask = g_tMask.Load( int3( pixelCoord, 0 ) );
        float2 edgeMask = g_tMask.Load( int3( nearestEdge, 0 ) );

        float currentRegionId = currentMask.r;
        float edgeRegionId = edgeMask.r;
        float blendFactor = ( currentMask.g + edgeMask.g ) * 0.5;

        if ( blendFactor <= 0 )
            return float4( g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb, 1 );

        // Compute blend weight early — skip expensive gather/depth work if negligible
        float2 edgeOffset = float2( nearestEdge ) - float2( pixelCoord );
        float edgeDist = length( edgeOffset );
        float adjustedRadius = ScreenBlendRadius * blendFactor;
        float falloff = 1.0 - saturate( edgeDist / max( adjustedRadius, 0.001 ) );
        float spatialWeight = 0.5 * smoothstep( 0.0, 1.0, falloff );

        if ( spatialWeight <= 0.001 )
            return float4( g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb, 1 );

        // Past all early-outs — load color and do the expensive work
        float3 originalColor = g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb;
        float3 worldPos = Depth::GetWorldPosition( float2( pixelCoord ) );
        float depthThreshold = max( blendFactor * 8.0, 0.001 );

        // Mirror UV across the seam boundary
        float2 mirrorPixel = float2( pixelCoord ) + edgeOffset * 2.0;
        float2 mirrorUv = ( mirrorPixel + 0.5 ) * g_vInvViewportSize;

        // Gather color and region IDs at mirror position
        float4 gatheredRed = g_tColorBuffer.GatherRed( g_sLinearClamp, mirrorUv );
        float4 gatheredGreen = g_tColorBuffer.GatherGreen( g_sLinearClamp, mirrorUv );
        float4 gatheredBlue = g_tColorBuffer.GatherBlue( g_sLinearClamp, mirrorUv );
        float4 gatheredIds = g_tMask.GatherRed( g_sLinearClamp, mirrorUv );

        float2 mirrorBase = floor( mirrorPixel );

        float3 mirrorColor = float3( 0, 0, 0 );
        float validSamples = 0;
        float depthWeight = 0;

        // Weight each gathered sample by region match and depth proximity
        [unroll]
        for ( uint j = 0; j < 4; j++ )
        {
            float sampleId = gatheredIds[j];

            if ( sampleId != currentRegionId && sampleId == edgeRegionId )
            {
                float2 samplePixel = mirrorBase + gatherOffsets[j];
                float3 sampleWorldPos = Depth::GetWorldPosition( samplePixel );

                float diff = length( sampleWorldPos - worldPos );
                depthWeight += saturate( 1.0 - diff / depthThreshold );

                mirrorColor += float3( gatheredRed[j], gatheredGreen[j], gatheredBlue[j] );
                validSamples += 1.0;
            }
        }

        if ( validSamples == 0 )
            return float4( originalColor, 1 );

        mirrorColor /= validSamples;
        depthWeight /= validSamples;

        float blendWeight = spatialWeight * depthWeight;

        return float4( lerp( originalColor, mirrorColor, blendWeight ), 1 );
    }
}
