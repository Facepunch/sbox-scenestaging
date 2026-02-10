// Mesh Blend Debug Shader
// Visualizes intermediate pass buffers for debugging

HEADER
{
    Description = "Mesh Blend Debug";
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

    Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( false ); >;
    Texture2D<float2> g_tMask < Attribute( "MaskTexture" ); >;
    Texture2D<uint2> g_tEdgeMap < Attribute( "EdgeMap" ); >;
    int g_nDebugMode < Attribute( "DebugMode" ); Default( 0 ); >;

    static const uint INVALID_EDGE_VALUE = 0xFFFFFFFF;

    // Hash a normalized region ID to a distinct color for visualization
    float3 RegionIdToColor( float id )
    {
        if ( id <= 0 )
            return float3( 0, 0, 0 );

        // Simple hash to get varied hues per region
        float h = frac( id * 7.13 + 0.5 );
        float3 col;
        col.r = abs( h * 6.0 - 3.0 ) - 1.0;
        col.g = 2.0 - abs( h * 6.0 - 2.0 );
        col.b = 2.0 - abs( h * 6.0 - 4.0 );
        return saturate( col ) * 0.8 + 0.2;
    }

    float4 MainPs( PixelInput input ) : SV_Target0
    {
        uint2 pixelCoord = uint2( input.vPositionSs.xy );
        float3 originalColor = g_tColorBuffer.Load( int3( pixelCoord, 0 ) ).rgb;

        // 1 = Mask, 2 = Edges, 3 = JFA
        if ( g_nDebugMode == 1 )
        {
            // Mask: show region ID as color, blend falloff as brightness
            float2 maskData = g_tMask.Load( int3( pixelCoord, 0 ) );
            float regionId = maskData.r;
            float falloff = maskData.g;

            if ( regionId <= 0 )
                return float4( originalColor * 0.3, 1 );

            float3 idColor = RegionIdToColor( regionId );
            return float4( lerp( originalColor * 0.3, idColor, 0.7 + falloff * 0.3 ), 1 );
        }
        else if ( g_nDebugMode == 2 )
        {
            // Edges: highlight pixels that have a valid nearest edge
            uint2 nearestEdge = g_tEdgeMap.Load( int3( pixelCoord, 0 ) );
            bool hasEdge = !any( nearestEdge == INVALID_EDGE_VALUE );

            float2 maskData = g_tMask.Load( int3( pixelCoord, 0 ) );
            bool hasMask = maskData.r > 0;

            if ( !hasEdge )
                return float4( originalColor * 0.3, 1 );

            // Show edge pixels in bright yellow, nearby influence in orange
            float edgeDist = length( float2( nearestEdge ) - float2( pixelCoord ) );
            if ( edgeDist < 1.5 )
                return float4( 1.0, 1.0, 0.0, 1 ); // Edge pixel itself

            float3 edgeColor = hasMask ? float3( 1.0, 0.5, 0.0 ) : float3( 0.0, 0.5, 1.0 );
            return float4( lerp( originalColor * 0.3, edgeColor, 0.6 ), 1 );
        }
        else if ( g_nDebugMode == 3 )
        {
            // JFA: visualize distance to nearest edge as smooth gradient
            uint2 nearestEdge = g_tEdgeMap.Load( int3( pixelCoord, 0 ) );

            if ( any( nearestEdge == INVALID_EDGE_VALUE ) )
                return float4( originalColor * 0.3, 1 );

            float edgeDist = length( float2( nearestEdge ) - float2( pixelCoord ) );
            float normalizedDist = saturate( edgeDist / 32.0 );

            // Smooth white-to-black ramp so any JFA errors are obvious
            float3 gradient = lerp( float3( 1, 1, 1 ), float3( 0, 0, 0 ), normalizedDist );

            return float4( gradient, 1 );
        }

        return float4( originalColor, 1 );
    }
}
