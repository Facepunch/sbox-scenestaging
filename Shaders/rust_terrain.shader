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

    Feature( F_SPLATMAP, 0..1 ( 0 = "4 Layer Splat", 1 = "8 Layer Splat" ), "Number of splat layer" );
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
    CreateTexture2DWithoutSampler( g_tSplatMap0 ) < Attribute( "SplatMap0" ); SrgbRead( false ); >;
    CreateTexture2DWithoutSampler( g_tSplatMap1 ) < Attribute( "SplatMap1" ); SrgbRead( false ); >;    
    CreateTexture2DWithoutSampler( g_tBiomeMap ) < Attribute( "BiomeMap" ); SrgbRead( false ); >;

    // Used to sample the heightmap
    SamplerState g_sBilinearBorder < Filter( BILINEAR ); AddressU( BORDER ); AddressV( BORDER ); >;

    // Used to sample textures
    SamplerState g_sAnisotropic < Filter( ANISOTROPIC ); MaxAniso(8); >;

    float g_flHeightScale < Attribute( "HeightScale" ); Default( 1024.0f ); >;
    float g_flTerrainResolution < Attribute( "TerrainResolution" ); Default( 40.0f ); >;
    int g_nDebugView < Attribute( "DebugView" ); >;

    float GetHeight( float2 uv )
    {
        return Tex2DLevelS( g_tHeightMap, g_sBilinearBorder, uv, 0 ).r;
        // float3 sample = Tex2DLevelS( g_tHeightMap, g_sBilinearBorder, uv, 0 ).rgb;
        // return ( ( sample.b * 256 + sample.r ) / 256) * 2;
    }
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

    #include "rust_terrain.hlsl"
    #include "TerrainCommon.hlsl"

    StaticCombo( S_MODE_TOOLS_WIREFRAME, 0..1, Sys( ALL ) );
    StaticCombo( S_MODE_DEPTH, 0..1, Sys( ALL ) );

    #define DEFINE_SPLATMAP(index) \
    CreateInputTexture2D( TextureColor##index,            Srgb,   8,  "",                 "_color",   TOKEN_STRINGIZE(Splat index) , Default3( 1.0, 1.0, 1.0 ) ); \
    CreateInputTexture2D( TextureNormal##index,           Linear, 8,  "NormalizeNormals", "_normal",  TOKEN_STRINGIZE(Splat index) , Default3( 0.5, 0.5, 1.0 ) ); \
    CreateInputTexture2D( TextureRoughness##index,        Linear, 8, "",                  "_rough",   TOKEN_STRINGIZE(Splat index) , Default( 0.5 ) ); \
    CreateInputTexture2D( TextureMetalness##index,        Linear, 8, "",                  "_metal",   TOKEN_STRINGIZE(Splat index) , Default( 1.0 ) ); \
    CreateInputTexture2D( TextureAmbientOcclusion##index, Linear, 8, "",                  "_ao",      TOKEN_STRINGIZE(Splat index) , Default( 1.0 ) ); \
    CreateTexture2DWithoutSampler( g_tColor##index ) < Channel( RGB, Box( TextureColor##index ), Srgb ); OutputFormat( DXT1 ); SrgbRead( true ); >; \
    CreateTexture2DWithoutSampler( g_tNormal##index ) < Channel( RGB, Box( TextureNormal##index ), Linear ); OutputFormat( DXT5 ); SrgbRead( false ); >; \
    CreateTexture2DWithoutSampler( g_tRma##index )    < Channel( R,    Box( TextureRoughness##index ), Linear ); Channel( G, Box( TextureMetalness##index ), Linear ); Channel( B, Box( TextureAmbientOcclusion##index ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

    DEFINE_SPLATMAP(0)
    DEFINE_SPLATMAP(1)
    DEFINE_SPLATMAP(2)
    DEFINE_SPLATMAP(3)
    DEFINE_SPLATMAP(4)
    DEFINE_SPLATMAP(5)
    DEFINE_SPLATMAP(6)
    DEFINE_SPLATMAP(7)

	#if ( S_MODE_TOOLS_WIREFRAME )
		RenderState( FillMode, WIREFRAME );
		RenderState( SlopeScaleDepthBias, -0.5 ); // Depth bias params tuned for plantation_source2 under DX11
		RenderState( DepthBiasClamp, -0.0005 );
	#endif

    // uvmix input => x: mult, y: start, z: rcp_dist

    float UVMIX( float camDist, float3 uvmix )
    {
        return saturate( ( camDist - uvmix.y ) * uvmix.z );
    }

    float4 Splat( Texture2D tex, float2 uv, float2 uvFar, float freqLerp )
    {
        float4 freq1 = Tex2DS( tex, g_sAnisotropic, uv );
        float4 freq2 = Tex2DS( tex, g_sAnisotropic, uvFar );
        return lerp( freq1, freq2, freqLerp );
    }

    void FetchSplatCombine( int i, float3 worldPos, float2 uv, float weight, float4 biome, inout float3 outCol, inout float3 outNrm, inout float3 outRma )
    {
        const float4x4 layerTintMatrix = float4x4( layerAridColor[ i ], layerTemperateColor[ i ], layerTundraColor[ i ], layerArcticColor[ i ] );

        Texture2D ColorTextures[8] = { g_tColor0, g_tColor1, g_tColor2, g_tColor3, g_tColor4, g_tColor5, g_tColor6, g_tColor7 };
        Texture2D NormalTextures[8] = { g_tNormal0, g_tNormal1, g_tNormal2, g_tNormal3, g_tNormal4, g_tNormal5, g_tNormal6, g_tNormal7 };
        Texture2D RmaTextures[8] = { g_tRma0, g_tRma1, g_tRma2, g_tRma3, g_tRma4, g_tRma5, g_tRma6, g_tRma7 };

        const float2 uv_near = worldPos.xy * ( layerUV[ i ] * 39 );
     	const float3 uvmix = layerUVMIX[ i ];
		const float2 uv_far = uv_near * uvmix.x;
        const float freq = UVMIX( distance( worldPos, g_vCameraPositionWs ), uvmix );

        float3 col, nrm, rma;
        if ( 1 ) // Blink( 1.0 ) )
        {
            // col = float3( freq, 0, 0 );
            col = Splat( ColorTextures[i], uv_near, uv_far, freq ).rgb;
            nrm = Splat( NormalTextures[i], uv_near, uv_far, freq ).rgb;
            rma = Splat( RmaTextures[i], uv_near, uv_far, freq ).rgb;
        }
        else
        {
            col = Tex2DS( ColorTextures[i], g_sAnisotropic, uv_near ).rgb;
            nrm = Tex2DS( NormalTextures[i], g_sAnisotropic, uv_near ).rgb;
            rma = Tex2DS( RmaTextures[i], g_sAnisotropic, uv_near ).rgb;
        }

        col *= mul( biome, layerTintMatrix ).rgb;

        float blend_top = saturate( weight * 2 );
		blend_top *= blend_top;

        // float blend_top = saturate( pow( max( 0, weight * nrm_top.g ), layerFalloff[ i ] ) );

        outCol = lerp( outCol, col, blend_top );
        outNrm = lerp( outNrm, nrm, blend_top );
        outRma = lerp( outRma, rma, blend_top );
    }

    void SplatmapMix( float2 vUV, float3 worldPos, out float3 vMixedAlbedo, out float3 vMixedNormal, out float3 vMixedRma )
    {
        // sample biome texture
        float4 biome = Tex2DS( g_tBiomeMap, g_sAnisotropic, vUV );

        // sample control textures
        float4 splat_control0 = Tex2DS( g_tSplatMap0, g_sAnisotropic, vUV );
        float4 splat_control1 = Tex2DS( g_tSplatMap1, g_sAnisotropic, vUV );

        // renormalize splat control maps
        float sum = dot( splat_control0, splat_control0 ) + dot( splat_control1, splat_control1 );
        float rcpLen = ( sum > 0.00001 ) ? 1.0f / sqrt( sum ) : 0;
        splat_control0 *= rcpLen;
        splat_control1 *= rcpLen;

        const float weights[ 8 ] = { splat_control0.r, splat_control0.g, splat_control0.b, splat_control0.a, splat_control1.r, splat_control1.g, splat_control1.b, splat_control1.a };

        vMixedAlbedo = float3( 1, 1, 1 );
        vMixedNormal = float3( 1, 1, 1 );
        vMixedRma = float3( 1, 1, 1 );

        for ( int i = 0; i < 8; i++ )
        {
            const float weight = weights[ i ] * ( 1.0 + layerFactor[ i ] );
            const float t_max = saturate( pow( max( 0, weight ), layerFalloff[ i ] ) );

            if ( t_max > 0 )
            {
                FetchSplatCombine( i, worldPos, vUV, weight, biome, vMixedAlbedo, vMixedNormal, vMixedRma );
            }
        }
    }

    float4 WireframeColor( PixelInput i )
    {
        // black wireframe if we're looking at lods
        if ( g_nDebugView == 1 )
        {
            return float4( 0, 0, 0, 1 );
        }
        else
        {
            float3 hsv = float3( i.LodLevel / 10.0f, 0.6f, 1.0f );
            return float4( SrgbGammaToLinear( HsvToRgb( hsv ) ), 1.0f );
        }
    }

	//
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
        // float2 controlUv = i.WorldPosition.xy / 2048.0;
        // float2 splatUv = i.WorldPosition.xy / 

        float2 texSize = TextureDimensions2D( g_tHeightMap, 0 );
        float2 uv = i.WorldPosition.xy / ( texSize * g_flTerrainResolution );
        // uv.x = 1.0f - uv.x;


        if ( uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0 )
        {
            clip( -1 );
            return float4( 0, 0, 0, 0 );
        }

        #if ( S_MODE_TOOLS_WIREFRAME )
           return WireframeColor( i );
        #endif        

        // calculate normals
        float3 geoNormal = GetNormal( g_tHeightMap, uv );

        float3 vColor, vNormalTs, vRma;
        SplatmapMix( uv, i.WorldPosition, vColor, vNormalTs, vRma );

        float3 vTangentUWs = normalize( cross( geoNormal, float3( 0, -1, 0 ) ) ); 
        float3 vTangentVWs = normalize( cross( geoNormal, -vTangentUWs ) );

        Material p = Material::Init();
        p.Albedo = vColor;
        p.Normal = TransformNormal( DecodeNormal( vNormalTs ), geoNormal, vTangentUWs, vTangentVWs );
        p.Roughness = vRma.r;
        p.Metalness = vRma.g;
        p.AmbientOcclusion = vRma.b;
        p.TextureCoords = uv;

        p.WorldPosition = i.WorldPosition;
        p.WorldPositionWithOffset = i.WorldPosition;
        p.ScreenPosition = i.ScreenPosition;
        p.GeometricNormal = geoNormal;

        p.WorldTangentU = vTangentUWs;
        p.WorldTangentV = vTangentVWs;

        if ( g_nDebugView != 0 )
        {
            return Debug( i, p );
        }

	    return ShadingModelStandard::Shade( p );
	}
}