// Mesh Blend Mask Shader
// Rasterizes blend targets to mask RT, outputting region ID and blend falloff.

HEADER
{
    Description = "Mesh Blend Mask Pass";
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
    #include "system.fxc"
    #include "vr_common.fxc"
}

struct VS_INPUT
{
    float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
    uint nInstanceTransformID : TEXCOORD13 < Semantic( InstanceTransformUv ); >;
    uint nBoneIndex : BLENDINDICES < Semantic( BlendIndices ); >;
};

struct PS_INPUT
{
    float4 vPositionPs : SV_Position;
    float3 vPositionWs : TEXCOORD0;
};

VS
{
    #include "instancing.fxc"

    PS_INPUT MainVs( VS_INPUT i )
    {
        PS_INPUT o;
        float3x4 matObjectToWorld = GetTransformMatrix( i.nInstanceTransformID, i.nBoneIndex.x );
        float3 vPositionWs = mul( matObjectToWorld, float4( i.vPositionOs.xyz, 1.0 ) );
        o.vPositionWs = vPositionWs;
        o.vPositionPs = Position3WsToPs( vPositionWs.xyz );
        return o;
    }
}

PS
{
    // Per-draw attributes
    int RegionId < Attribute( "RegionId" ); Default( 0 ); >;
    float BlendFalloff < Attribute( "BlendFalloff" ); Default( 1.0 ); >;

    float2 MainPs( PS_INPUT i ) : SV_Target0
    {
        // Normalize region ID - use frac to get consistent value for equality comparison
        float normalizedId = frac( float( RegionId ) / 65535.0 );
        if ( normalizedId == 0 ) normalizedId = 1.0 / 65535.0; // Ensure non-zero for valid regions
        
        return float2( normalizedId, BlendFalloff );
    }
}
