//-----------------------------------------------------------------------------
// Feature includes
//-----------------------------------------------------------------------------
FEATURES
{
    #include "common/features.hlsl"
}

//-----------------------------------------------------------------------------
// Render modes
//-----------------------------------------------------------------------------
MODES
{
    Forward();
}

//-----------------------------------------------------------------------------
// Common includes
//-----------------------------------------------------------------------------
COMMON
{
    #include "common/shared.hlsl"
}

//-----------------------------------------------------------------------------
// Vertex input structure
//-----------------------------------------------------------------------------
struct VertexInput
{
    float4 PositionOs           : POSITION    < Semantic( Position ); >;
    uint   nInstanceTransformID  : TEXCOORD13 < Semantic( InstanceTransformUv ); >;
    
    #if ( D_MORPH ) || ( D_CS_VERTEX_ANIMATION )
        float nVertexIndex      : TEXCOORD14 < Semantic( MorphIndex ); >;
        float VertexCacheIndex : TEXCOORD15 < Semantic( MorphIndex ); >;
    #endif
};

//-----------------------------------------------------------------------------
// Pixel input structure
//-----------------------------------------------------------------------------
struct PixelInput
{
    float4 PositionPs : SV_Position;
    float3 VelocityPs : TEXCOORD0;
};

//-----------------------------------------------------------------------------
// Vertex shader
//-----------------------------------------------------------------------------
VS
{
    #include "vr_common_vs_code.fxc"

    DynamicComboRule( Allow0( D_SKINNING ) ); // Allow exclusively compute skinning

    PixelInput MainVs( VertexInput input )
    {
        PixelInput output;
        
        // Generate Object->World matrix and animation scale
        ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( input );
        float3 worldPosition;
        
        #if ( D_CS_VERTEX_ANIMATION )
            // Animated vertex cache path
            uint vertexId = extraShaderData.nUnused + uint( input.VertexCacheIndex );
            CachedAnimatedVertex_t vertex = FetchCachedVertex( vertexId );

            float3 normal;
            float3 tangent;
            float sign;
            DecodeCachedVertexTangentSpace( vertex, normal, tangent, sign );

            worldPosition = vertex.vPosWs;
        #else
            // Standard transformation path
            float3x4 objectToWorldMatrix = CalculateInstancingObjectToWorldMatrix( input );
            float3 animationScale = CalculateInstancingAnimationScale( input );
            worldPosition = mul( objectToWorldMatrix, float4( input.PositionOs.xyz * animationScale.xyz, 1.0 ) );
        #endif

        output.PositionPs = Position3WsToPs( worldPosition );
        output.VelocityPs = float3( 0.0f, 0.0f, 0.0f );
        
        return output;
    }
}

//-----------------------------------------------------------------------------
// Pixel shader
//-----------------------------------------------------------------------------
PS
{
    #include "common/pixel.hlsl"

    float2 MainPs( PixelInput input ) : SV_Target0
    {
        return float2( 1.0f, 1.0f );
    }
}