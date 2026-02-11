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
    Texture2D<uint2> g_tEdgeMap < Attribute( "EdgeMap" ); >;

    // Resolution downscale factor: 1 = full, 2 = half, 4 = quarter.
    // Mask and edge map are at reduced resolution; color/depth are full-res.
    int ResolutionScale < Attribute( "ResolutionScale" ); Default( 1 ); >;

    static const uint INVALID_EDGE_VALUE = 0xFFFFFFFF;
    static const float ScreenBlendRadius = 64.0;

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
        float flScale = float( max( ResolutionScale, 1 ) );

        // Map full-res pixel to reduced-res texel
        uint2 maskPixel = pixelCoord / uint2( max( ResolutionScale, 1 ), max( ResolutionScale, 1 ) );

        // Always get original color at full resolution
        float3 originalColor = g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb;

        // Edge map is at reduced resolution
        uint2 nearestEdge = g_tEdgeMap.Load( int3( maskPixel, 0 ) );

        if ( any( nearestEdge == INVALID_EDGE_VALUE ) )
            return float4( originalColor, 1 );

        // World position from full-res depth
        float3 worldPos = Depth::GetWorldPosition( float2( pixelCoord ) );

        // Mask reads at reduced resolution
        float currentRegionId = g_tMask.Load( int3( maskPixel, 0 ) ).r;
        float edgeRegionId = g_tMask.Load( int3( nearestEdge, 0 ) ).r;

        float currentFalloff = g_tMask.Load( int3( maskPixel, 0 ) ).g;
        float edgeFalloff = g_tMask.Load( int3( nearestEdge, 0 ) ).g;
        float blendFactor = ( currentFalloff + edgeFalloff ) * 0.5;

        if ( blendFactor <= 0 )
            return float4( originalColor, 1 );

        float depthThreshold = max( blendFactor * 8.0, 0.001 );

        // Convert reduced-res edge position to full-res space
        float2 nearestEdgeFullRes = ( float2( nearestEdge ) + 0.5 ) * flScale - 0.5;
        float2 edgeOffset = nearestEdgeFullRes - float2( pixelCoord );
        float edgeDist = length( edgeOffset );

        // Mirror UV across the seam boundary (full-res space)
        float2 mirrorPixel = float2( pixelCoord ) + edgeOffset * 2.0;
        float2 mirrorUv = ( mirrorPixel + 0.5 ) * g_vInvViewportSize;

        // Gather color at full-res mirror position
        float4 gatheredRed = g_tColorBuffer.GatherRed( g_sLinearClamp, mirrorUv );
        float4 gatheredGreen = g_tColorBuffer.GatherGreen( g_sLinearClamp, mirrorUv );
        float4 gatheredBlue = g_tColorBuffer.GatherBlue( g_sLinearClamp, mirrorUv );

        // Gather region IDs from reduced-res mask (UV is resolution-independent)
        float4 gatheredIds = g_tMask.GatherRed( g_sLinearClamp, mirrorUv );

        // Compute mirror base in reduced-res space for depth lookups
        float2 mirrorMaskPixel = mirrorPixel / flScale;
        float2 mirrorMaskBase = floor( mirrorMaskPixel );

        float3 mirrorColor = float3( 0, 0, 0 );
        float validSamples = 0;
        float depthWeight = 0;

        // Weight each gathered sample by region match and depth proximity
        for ( uint j = 0; j < 4; j++ )
        {
            float sampleId = gatheredIds[j];

            bool isDifferentRegion = sampleId != currentRegionId;
            bool matchesEdgeRegion = sampleId == edgeRegionId;

            if ( isDifferentRegion && matchesEdgeRegion )
            {
                // Map gathered mask texel center back to full-res for depth comparison
                float2 sampleMaskPixel = mirrorMaskBase + gatherOffsets[j];
                float2 sampleFullRes = ( sampleMaskPixel + 0.5 ) * flScale - 0.5;
                float3 sampleWorldPos = Depth::GetWorldPosition( sampleFullRes );

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

        // Screen-space blend radius, scaled by depth so it shrinks when zoomed out.
        float viewDist = length( worldPos - g_vCameraPositionWs );
        float distanceScale = min( 50.0 / max( viewDist, 1.0 ), 1.0 );
        float adjustedRadius = ScreenBlendRadius * distanceScale * blendFactor;

        // Strongest at edge (0.5), falls off with distance
        float blendWeight = saturate( 0.5 - edgeDist / max( adjustedRadius, 0.001 ) ) * depthWeight;

        return float4( lerp( originalColor, mirrorColor, blendWeight ), 1 );
    }
}
