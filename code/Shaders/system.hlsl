#include "vr_common.fxc"

//
// Helpers
//

static const float ToDegrees = 57.2958f;
static const float ToRadians = 0.0174533f;


//
// Transforms / Instancing
//

#define NUM_PIXELS_PER_TRANSFORM 4
StructuredBuffer< float4 > g_flTransformData < Attribute( "g_TransformBuffer" ); >;

struct ExtraShaderData_t
{
    float4 vTint;
    uint nUnused; //nEnvMapIndices;			// envmap << 8 | cube index
    uint nBlendWeightCount;			// if D_SKINNING, blend weight count
};

int GetInstanceOffset( uint instance ) { return instance * NUM_PIXELS_PER_TRANSFORM; }

float3x4 InstanceTransform( uint instance )
{
    uint slot = GetInstanceOffset( instance );
    return float3x4( g_flTransformData[ slot ], g_flTransformData[ slot + 1 ], g_flTransformData[ slot + 2 ] );
}

ExtraShaderData_t System_GetExtraPerInstanceShaderData( uint instance )
{
    float4 vRet;

    int nOffset = GetInstanceOffset( instance );
    vRet = g_flTransformData[ nOffset + 3 ];

    // Tint RGBA 8888, + envmap
    // vRet is packed [ alpha, rgb888, indices, _ ]
    // see the details in CreateExtraShaderData_TintRGBA32_EnvMap

    uint tint_rgb888 = asuint( vRet.y );
    ExtraShaderData_t extraShaderData;
    extraShaderData.vTint.w = vRet.x;
    extraShaderData.vTint.x = ( ( tint_rgb888 >> 16 ) & 0xff ) / 255.0f;
    extraShaderData.vTint.y = ( ( tint_rgb888 >>  8 ) & 0xff ) / 255.0f;
    extraShaderData.vTint.z = ( ( tint_rgb888 >>  0 ) & 0xff ) / 255.0f;
    //extraShaderData.nEnvMapIndices = asuint( vRet.z ) & 0xffff; //Now unused
    extraShaderData.nUnused = 0;
    extraShaderData.nBlendWeightCount = clamp( asuint( vRet.w )  & 0xffff, 0, 4 ); // Clamp so this doesnt crash the driver if corrupt
    return extraShaderData;
}

CreateAttributeTexture2DWithoutSampler( g_tBlueNoise ) : register( t0 ) < Attribute( "BlueNoise" ); SrgbRead( false ); >;

void OpaqueFadeDepth( float flOpacity, float2 vPositionSs )
{
	float flNoise = Tex2DLoad( g_tBlueNoise, int3( vPositionSs.xy % TextureDimensions2D( g_tBlueNoise, 0 ).xy, 0 ) ).g;
	clip( mad( flOpacity, 2.0, -1.5 ) + flNoise );
}

//
//
// SHEET SAMPLING
//
//

#include "sheet_sampling.fxc"

CreateTexture2D( g_SheetTexture ) < Attribute( "SheetTexture" ); Filter( MIN_MAG_MIP_POINT ); AddressU( WRAP ); AddressV( WRAP ); SrgbRead( false ); >;

//
// Most basic implementation. Get the bounds for a single
//
float4 SampleSheet(float4 data, float sequence, float time)
{
    if ( data.w == 0 )
        return float4(0, 0, 1, 1);
	
    SheetDataSamplerParams_t params;
    params.m_flSheetTextureBaseV = data.x;
    params.m_flOOSheetTextureWidth = 1.0f / data.y;
    params.m_flOOSheetTextureHeight = data.z;
    params.m_flSheetTextureWidth = data.y;
    params.m_flSheetSequenceCount = data.w;
    params.m_flSequenceAnimationTimescale = 1.0f;
    params.m_flSequenceIndex = sequence;
    params.m_flSequenceAnimationTime = time;

    SheetDataSamplerOutput_t o = SampleSheetData(PassToArgTexture2D(g_SheetTexture), params, false);
		
    float4 b = o.m_vFrame0Bounds;
    b.zw -= b.xy;
    
    return b;
}