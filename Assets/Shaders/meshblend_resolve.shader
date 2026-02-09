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
    SamplerState g_sLinearClamp < Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;
    Texture2D<float2> g_tMask < Attribute( "MaskTexture" ); >;
    Texture2D<uint2> g_tEdgeMap < Attribute( "EdgeMap" ); >;
    
    static const uint INVALID_EDGE_VALUE = 0xFFFFFFFF;
    static const float ScreenBlendRadius = 64.0;
    static const float ID_EPSILON = 0.001;

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
        
        // Always get original color first
        float3 originalColor = g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb;
        
        uint2 nearestEdge = g_tEdgeMap.Load( int3( pixelCoord, 0 ) );
        
        if ( any( nearestEdge == INVALID_EDGE_VALUE ) )
            return float4( originalColor, 1 );
        
        // Reconstruct world position for depth comparison
        float3 worldPos = Depth::GetWorldPosition( float2( pixelCoord ) );
        
        float currentRegionId = g_tMask.Load( int3( pixelCoord, 0 ) ).r;
        float edgeRegionId = g_tMask.Load( int3( nearestEdge, 0 ) ).r;
        
        // Average blend falloff from both sides of the seam
        float currentFalloff = g_tMask.Load( int3( pixelCoord, 0 ) ).g;
        float edgeFalloff = g_tMask.Load( int3( nearestEdge, 0 ) ).g;
        float blendFactor = ( currentFalloff + edgeFalloff ) * 0.5;
        
        // If both objects have 0 falloff, no blending
        if ( blendFactor <= 0 )
            return float4( originalColor, 1 );
        
        // Falloff controls depth rejection threshold
        float depthThreshold = max( blendFactor * 8.0, 0.001 );
        
        float2 edgeOffset = float2( nearestEdge ) - float2( pixelCoord );
        float edgeDist = length( edgeOffset );
        
        // Mirror UV across the seam boundary
        float2 mirrorPixel = float2( pixelCoord ) + edgeOffset * 2.0;
        float2 mirrorUv = ( mirrorPixel + 0.5 ) * g_vInvViewportSize;
        float2 mirrorBase = floor( mirrorPixel );
        
        // Gather color and region ID at mirror position
        float4 gatheredRed = g_tColorBuffer.GatherRed( g_sLinearClamp, mirrorUv );
        float4 gatheredGreen = g_tColorBuffer.GatherGreen( g_sLinearClamp, mirrorUv );
        float4 gatheredBlue = g_tColorBuffer.GatherBlue( g_sLinearClamp, mirrorUv );
        float4 gatheredIds = g_tMask.GatherRed( g_sLinearClamp, mirrorUv );
        
        float3 mirrorColor = float3( 0, 0, 0 );
        float validSamples = 0;
        float depthWeight = 0;
        
        float currentDepth = length( worldPos - g_vCameraPositionWs );
        
        // Weight each gathered sample by region match and depth proximity
        // Color averaged equally, depth weight only affects final blend strength.
        for ( uint j = 0; j < 4; j++ )
        {
            float sampleId = gatheredIds[j];
            
            // Only blend from the matching edge region
            bool isDifferentRegion = abs( sampleId - currentRegionId ) > ID_EPSILON;
            bool matchesEdgeRegion = abs( sampleId - edgeRegionId ) < ID_EPSILON;
            
            if ( isDifferentRegion && matchesEdgeRegion )
            {
                float2 samplePixel = mirrorBase + gatherOffsets[j];
                float3 sampleWorldPos = Depth::GetWorldPosition( samplePixel );
                
                // Linear depth weighting (only used for final blend strength)
                float sampleDepth = length( sampleWorldPos - g_vCameraPositionWs );
                float depthDiff = abs( sampleDepth - currentDepth );
                depthWeight += saturate( 1.0 - depthDiff / depthThreshold );
                
                mirrorColor += float3( gatheredRed[j], gatheredGreen[j], gatheredBlue[j] );
                validSamples += 1.0;
            }
        }
        
        if ( validSamples == 0 )
            return float4( originalColor, 1 );
        
        mirrorColor /= validSamples;
        depthWeight /= validSamples;
        
        // Screen-space blend radius, scaled by depth so it shrinks when zoomed out.
        // Clamped to avoid excessive radius at close range.
        float viewDist = length( worldPos - g_vCameraPositionWs );
        float distanceScale = min( 50.0 / max( viewDist, 1.0 ), 1.0 );
        float adjustedRadius = ScreenBlendRadius * distanceScale * blendFactor;
        
        // Strongest at edge (0.5), falls off with distance
        float blendWeight = saturate( 0.5 - edgeDist / max( adjustedRadius, 0.001 ) ) * depthWeight;
        
        return float4( lerp( originalColor, mirrorColor, blendWeight ), 1 );
    }
}
