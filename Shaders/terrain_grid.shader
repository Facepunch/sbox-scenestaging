//
// Simple Terrain shader without any splats or textures
//

HEADER
{
	Description = "Terrain";
    DevShader = true;
    DebugInfo = false;
}

FEATURES
{
    // gonna go crazy the amount of shit this stuff adds and fails to compile without
    #include "vr_common_features.fxc"
}

MODES
{
    VrForward();
    Depth( S_MODE_DEPTH );
    ToolsVis( S_MODE_TOOLS_VIS );
    ToolsWireframe( S_MODE_TOOLS_WIREFRAME );
}

COMMON
{
    // Opt out of stupid shit
    #define CUSTOM_MATERIAL_INPUTS
    #define CUSTOM_TEXTURE_FILTERING

    #include "common/shared.hlsl"

    CreateTexture2DWithoutSampler( g_tHeightMap ) < Attribute( "HeightMap" ); SrgbRead( false ); >;
    CreateTexture2DWithoutSampler( g_tControlMap ) < Attribute( "ControlMap" ); SrgbRead( false ); >;

    // Used to sample the heightmap
    SamplerState g_sBilinearBorder < Filter( BILINEAR ); AddressU( BORDER ); AddressV( BORDER ); >;

    float g_flHeightScale < Attribute( "HeightScale" ); Default( 1024.0f ); >;
    float g_flTerrainResolution < Attribute( "TerrainResolution" ); Default( 40.0f ); >;
    int g_nDebugView < Attribute( "DebugView" ); >;
}

struct VertexInput
{
	float3 PositionAndLod : POSITION < Semantic( PosXyz ); >;
};

struct PixelInput
{
    float3 WorldPosition : TEXCOORD0;
    uint LodLevel : COLOR0;

    #if ( PROGRAM == VFX_PROGRAM_VS )
        float4 PixelPosition : SV_Position;
    #endif

    #if ( PROGRAM == VFX_PROGRAM_PS )
        float4 ScreenPosition : SV_Position;
    #endif
};

VS
{
    #include "TerrainClipmap.hlsl"

	PixelInput MainVs( VertexInput i )
	{
        PixelInput o;
        o.WorldPosition = TerrainClipmapSingleMesh( i.PositionAndLod, g_tHeightMap, g_flTerrainResolution );
	    o.PixelPosition = Position3WsToPs( o.WorldPosition.xyz );
        o.LodLevel = i.PositionAndLod.z;

		return o;
	}
}

//=========================================================================================================================

PS
{
    #include "common/material.hlsl"
    #include "common/shadingmodel.hlsl"

    #include "TerrainCommon.hlsl"

    #define DEFINE_SPLATMAP(index) \
    CreateInputTexture2D( TextureColor##index,            Srgb,   8,  "",                 "_color",   TOKEN_STRINGIZE(Splat index) , Default3( 1.0, 1.0, 1.0 ) ); \
    CreateInputTexture2D( TextureNormal##index,           Linear, 8,  "NormalizeNormals", "_normal",  TOKEN_STRINGIZE(Splat index) , Default3( 0.5, 0.5, 1.0 ) ); \
    CreateTexture2DWithoutSampler( g_tColor##index ) < Channel( RGB, Box( TextureColor##index ), Srgb ); OutputFormat( DXT1 ); SrgbRead( true ); >; \
    CreateTexture2DWithoutSampler( g_tNormal##index ) < Channel( RGB, Box( TextureNormal##index ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >; \

    DEFINE_SPLATMAP(0)
    DEFINE_SPLATMAP(1)

    SamplerState g_sAnisotropic < Filter( ANISOTROPIC ); MaxAniso(8); >;

    StaticCombo( S_MODE_TOOLS_WIREFRAME, 0..1, Sys( ALL ) );
    StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );

	#if ( S_MODE_TOOLS_WIREFRAME )
		RenderState( FillMode, WIREFRAME );
		RenderState( SlopeScaleDepthBias, -0.5 ); // Depth bias params tuned for plantation_source2 under DX11
		RenderState( DepthBiasClamp, -0.0005 );
	#endif

    float CheckersGradBox( in float2 p )
    {
        // filter kernel
        float2 w = fwidth(p) + 0.001;
        // analytical integral (box filter)
        float2 i = 2.0*(abs(frac((p-0.5*w)*0.5)-0.5)-abs(frac((p+0.5*w)*0.5)-0.5))/w;
        // xor pattern
        return 0.5 - 0.5*i.x*i.y;                  
    }

	//
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
        float2 texSize = TextureDimensions2D( g_tHeightMap, 0 );
        float2 uv = i.WorldPosition.xy / ( texSize * g_flTerrainResolution );

        if ( uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0 )
        {
            clip( -1 );
            return float4( 0, 0, 0, 0 );
        }

        #if ( S_MODE_TOOLS_WIREFRAME )
           return WireframeColor( i.LodLevel );
        #endif

        // calculate normals
        float3 TangentU, TangentV;
        float3 geoNormal = TerrainNormal( g_tHeightMap, uv, TangentU, TangentV );

        //
        // this is all wrong, just a quick mess to get anything basic showing
        //

        float4 control = Tex2DS( g_tControlMap, g_sBilinearBorder, uv );
        float control_sum = control.r + control.g + control.b + control.a;

        float rcpLen = ( control_sum > 0.00001 ) ? 1.0f / sqrt( control_sum ) : 0;
        control *= rcpLen;

        float baseWeight = saturate( 1.0f - control_sum );

        float chessboard = CheckersGradBox( i.WorldPosition.xy / 64 );
        float3 base = 0.4 + chessboard * float3(0.2, 0.2, 0.2);
        float3 col1 = Tex2DS( g_tColor0, g_sAnisotropic, i.WorldPosition.xy / 256 );
        float3 col2 = Tex2DS( g_tColor1, g_sAnisotropic, i.WorldPosition.xy / 256 );
        float3 nrm1 = DecodeNormal( Tex2DS( g_tNormal0, g_sAnisotropic, i.WorldPosition.xy / 256 ) );
        float3 nrm2 = DecodeNormal( Tex2DS( g_tNormal1, g_sAnisotropic, i.WorldPosition.xy / 256 ) );

        float3 color = base * baseWeight;
        float3 blend = col1 * control.r;
        blend += col2 * control.g;

        color += blend;

        // this definitely isn't how you blend normals
        float3 norm = float3( 0, 0, 1 ); // * ( 1.0f - weight ) + blend1nm * control.r );
       
        float roughness = 1.0f; // (1.0f - saturate( weight )) * (0.5f - chessboard * 0.2f) + 1.0f * control.r;

        Material p = Material::Init();
        p.Albedo = color;
        p.Normal = TransformNormal( norm, geoNormal, TangentU, TangentV );
        p.Roughness = roughness;
        p.Metalness = 0.0f;
        p.AmbientOcclusion = 1.0f;
        p.TextureCoords = uv;

        p.WorldPosition = i.WorldPosition;
        p.WorldPositionWithOffset = i.WorldPosition - g_vHighPrecisionLightingOffsetWs.xyz;
        p.ScreenPosition = i.ScreenPosition;
        p.GeometricNormal = geoNormal;

        p.WorldTangentU = TangentU;
        p.WorldTangentV = TangentV;

        if ( g_nDebugView != 0 )
        {
            return Debug( i, p );
        }

	    return ShadingModelStandard::Shade( p );
	}
}