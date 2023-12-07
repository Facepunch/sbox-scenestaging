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
    Feature( F_ADDITIVE_BLEND, 0..1, "Translucent" );

    Feature( F_SPLATMAP, 0..1 ( 0 = "4 Layer Splat", 1 = "8 Layer Splat" ), "Number of splat layer" );
}

MODES
{
    VrForward();													    // Indicates this shader will be used for main rendering
    Depth( S_MODE_DEPTH );
    ToolsVis( S_MODE_TOOLS_VIS ); 									    // Ability to see in the editor
    ToolsWireframe( S_MODE_TOOLS_WIREFRAME );
}

COMMON
{
    // Opt out of stupid shit
    #define CUSTOM_MATERIAL_INPUTS
    #define CUSTOM_TEXTURE_FILTERING

    #include "common/shared.hlsl"

    CreateTexture2DWithoutSampler( g_tHeightMap ) < Attribute( "Heightmap" ); SrgbRead( false ); >;
    // We should generate this in compute and sample it.
    // CreateTexture2D( g_tNormalMap ) < Attribute( "NormalMap" ); SrgbRead( false ); Filter( BILINEAR ); AddressU( BORDER ); AddressV( BORDER ); >;
    CreateTexture2DWithoutSampler( g_tSplatMap0 ) < Attribute( "SplatMap0" ); SrgbRead( false ); >;
    CreateTexture2DWithoutSampler( g_tSplatMap1 ) < Attribute( "SplatMap1" ); SrgbRead( false ); >;
    
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
    }
}

struct VertexInput
{
	float3 PositionAndLod : POSITION < Semantic( PosXyz ); >;
};

struct PixelInput
{
    float3 WorldPosition : TEXCOORD0;
    float2 TextureCoords : TEXCOORD2;

    #if ( PROGRAM != VFX_PROGRAM_PS ) 
        float4 vPositionPs : SV_Position;
    #else // PS only
        float4 ScreenPosition : SV_ScreenPosition;
    #endif

    float4 vDebugColor				: COLOR0;
};

VS
{
    static const float4 debugColors[8] = 
    {
        float4(0.8, 0.0, 0.0, 1.0), // Red
        float4(0.8, 0.4, 0.0, 1.0), // Orange
        float4(0.8, 0.8, 0.0, 1.0), // Yellow
        float4(0.0, 0.6, 0.0, 1.0), // Green
        float4(0.0, 0.5, 0.8, 1.0), // Light Blue / Cyan
        float4(0.0, 0.0, 0.8, 1.0), // Blue
        float4(0.4, 0.0, 0.8, 1.0), // Indigo
        float4(0.8, 0.0, 0.8, 1.0)  // Violet
    };

    float2 roundToIncrement(float2 value, float increment) {
        return round(value * (1.0 / increment)) * increment;
    }

	//
	// Main
	//
	PixelInput MainVs( VertexInput i )
	{
        float2 texSize = TextureDimensions2D( g_tHeightMap, 0 );

        float gridLevel = i.PositionAndLod.z;
        float mipMetersPerHeightfieldTexel = g_flTerrainResolution * exp2(gridLevel);
        float2 objectToWorld = roundToIncrement( g_vCameraPositionWs.xy, mipMetersPerHeightfieldTexel );

        PixelInput o;

        // move shit around
        o.WorldPosition.xy = i.PositionAndLod.xy * g_flTerrainResolution + objectToWorld;

        // sample heightmap and adjust
        float2 uv = ( o.WorldPosition.xy + ( texSize * g_flTerrainResolution ) / 2 ) / ( texSize * g_flTerrainResolution );
        uv.x = 1.0f - uv.x;

        float flHeight = GetHeight( uv );
        o.WorldPosition.z = flHeight * g_flHeightScale;

        o.TextureCoords = uv;
        o.vDebugColor = debugColors[clamp( gridLevel, 0, 7 )];

	    o.vPositionPs.xyzw = Position3WsToPs( o.WorldPosition.xyz );

		return o;
	}
}

//=========================================================================================================================

PS
{
    #include "common/material.hlsl"
    #include "common/shadingmodel.hlsl"

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
		RenderState( SlopeScaleDepthBias, -0.5 );
		RenderState( DepthBiasClamp, -0.0005 );
		RenderState( DepthWriteEnable, false );
		#define DEPTH_STATE_ALREADY_SET
	#endif

    //
    // Takes 8 samples
    // This is easy for now, an optimization would be to generate this once in a compute shader
    // Less texture sampling but higher memory requirements
    // This is between -1 and 1;
    //
    float3 GetNormal( Texture2D tHeightMap, float2 uv, float flUnitsPerTexel )
    {
        float2 texelSize = 1.0f / (float2)TextureDimensions2D( tHeightMap, 0 );

        float tl = abs( GetHeight( uv + texelSize * float2( -1, -1 ) ) );
        float  l = abs( GetHeight( uv + texelSize * float2( -1,  0 ) ) );
        float bl = abs( GetHeight( uv + texelSize * float2( -1,  1 ) ) );
        float  t = abs( GetHeight( uv + texelSize * float2(  0, -1 ) ) );
        float  b = abs( GetHeight( uv + texelSize * float2(  0,  1 ) ) );
        float tr = abs( GetHeight( uv + texelSize * float2(  1, -1 ) ) );
        float  r = abs( GetHeight( uv + texelSize * float2(  1,  0 ) ) );
        float br = abs( GetHeight( uv + texelSize * float2(  1,  1 ) ) );

        // Compute dx using Sobel:
        //           -1 0 1 
        //           -2 0 2
        //           -1 0 1
        float dX = tr + 2*r + br -tl - 2*l - bl;
    
        // Compute dy using Sobel:
        //           -1 -2 -1 
        //            0  0  0
        //            1  2  1
        float dY = bl + 2*b + br -tl - 2*t - tr;

        float normalStrength = 128;
        return normalize(float3(dX, dY * -1, 1.0f / normalStrength));
        // don't even think of doing * 0.5f + 0.5f;

        /*
        [6][7][8]
        [3][4][5]
        [0][1][2]
        */
        /*float s[9];
        s[0] = GetHeight( uv + float2( -vTexelSize.x, -vTexelSize.y ) );
        s[1] = GetHeight( uv + float2( 0, -vTexelSize.y ) );
        s[2] = GetHeight( uv + float2( vTexelSize.x, -vTexelSize.y ) );
        s[3] = GetHeight( uv + float2( -vTexelSize.x, 0 ) );
        s[5] = GetHeight( uv + float2( vTexelSize.x, 0 ) );
        s[6] = GetHeight( uv + float2( -vTexelSize.x, vTexelSize.y ) );
        s[7] = GetHeight( uv + float2( 0, vTexelSize.y ) );
        s[8] = GetHeight( uv + float2( vTexelSize.x, vTexelSize.y ) );

        float3 gradient = float3(s[3] - s[5], s[1] - s[7], 0.001 ); // The "2.0" is a scale factor for the normal strength
        return normalize( gradient ) * 0.5 + 0.5;

        float dx = s[8] + 2 * s[5] + s[2] - s[6] - 2 * s[3] - s[0];
        float dy = s[0] + 2 * s[1] + s[2] - s[6] - 2 * s[7] - s[8];

        float normalStr = 0.5;
        float3 N = normalize( float3( dx, 1.0 / normalStr, dy ) );
        return N * 0.5 + 0.5;*/

        // float3 vNormal;
        // vNormal.x = flUnitsPerTexel * -(3 * (s[2] - s[0]) + 10 * (s[5] - s[3]) + 3 * (s[8] - s[6]));
        // vNormal.y = flUnitsPerTexel * -(3 * (s[6] - s[0]) + 10 * (s[7] - s[1]) + 3 * (s[8] - s[2]));

        // vNormal.z = 1.0;
        // return normalize( vNormal ) * 0.5 + 0.5;
    }

    float4 Debug( PixelInput i, Material m )
    {
        if ( g_nDebugView == 1 )
        {
            return float4( SrgbGammaToLinear( PackToColor( m.GeometricNormal ) ), 1.0f );
        }

        if ( g_nDebugView == 2 )
        {
            return float4( SrgbGammaToLinear( PackToColor( m.Normal ) ), 1.0f );
        }

        if ( g_nDebugView == 3 )
        {
            return GetHeight( i.TextureCoords ).r;
        }

        if ( g_nDebugView == 4 )
        {
            return i.vDebugColor;
        }

        if ( g_nDebugView == 5 )
        {
            return float4( Tex2DS( g_tSplatMap0, g_sAnisotropic, i.TextureCoords ).rgb, 1.0f );
        }        

        return float4( 0, 0, 0, 1 );
    }

    void SplatmapMix( float2 vUV, out float3 vMixedAlbedo, out float3 vMixedNormal, out float3 vMixedRma )
    {
        float4 vSplat0 = Tex2DS( g_tSplatMap0, g_sAnisotropic, vUV );
        float4 vSplat1 = Tex2DS( g_tSplatMap1, g_sAnisotropic, vUV );


        float splatTotal = vSplat0.r + vSplat0.g + vSplat0.b + vSplat0.a + 
                           vSplat1.r + vSplat1.g + vSplat1.b + vSplat1.a;       

        // vSplat0 = vSplat0 * (1/splatTotal);
        // vSplat1 = vSplat1 * (1/splatTotal);

        float sum = dot( vSplat0, vSplat0 ) + dot( vSplat1, vSplat1 );
        float rcpLen = ( sum > 0.00001 ) ? 1.0f / sqrt( sum ) : 0;
        vSplat0 *= rcpLen;
        vSplat1 *= rcpLen;

        float2 vSplatUv = vUV * 512.0f;

        #define SPLAT_TEXTURE(index, splat) \
            float3 vColor##index = Tex2DS( g_tColor##index, g_sAnisotropic, vSplatUv ).rgb * splat; \
            float3 vNormal##index = Tex2DS( g_tNormal##index, g_sAnisotropic, vSplatUv ).rgb * splat; \
            float3 vRma##index = Tex2DS( g_tRma##index, g_sAnisotropic, vSplatUv ).rgb * splat;

        SPLAT_TEXTURE(0, vSplat0.r)
        SPLAT_TEXTURE(1, vSplat0.g)
        SPLAT_TEXTURE(2, vSplat0.b)
        SPLAT_TEXTURE(3, vSplat0.a)
        SPLAT_TEXTURE(4, vSplat1.r)
        SPLAT_TEXTURE(5, vSplat1.g)
        SPLAT_TEXTURE(6, vSplat1.b)
        SPLAT_TEXTURE(7, vSplat1.a)

        vMixedAlbedo = vColor0 + vColor1 + vColor2 + vColor3 + vColor4 + vColor5 + vColor6 + vColor7;
        vMixedNormal = vNormal0 + vNormal1 + vNormal2 + vNormal3 + vNormal4 + vNormal5 + vNormal6 + vNormal7;
        vMixedRma = vRma0 + vRma1 + vRma2 + vRma3 + vRma4 + vRma5 + vRma6 + vRma7;
    }

	//
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
        if ( i.TextureCoords.x < 0.0 || i.TextureCoords.y < 0.0 || i.TextureCoords.x > 1.0 || i.TextureCoords.y > 1.0 )
        {
            clip( -1 );
            return float4( 0, 0, 0, 1 );
        }

        // Depth
        /*#if ( S_MODE_DEPTH )
        {
            return float4( 0, 0, 0, 1 );
        }
        #endif*/

		#if ( S_MODE_TOOLS_WIREFRAME )
		{
			return float4( 0, 0, 0, 1 );
		}
        #endif

        // calculate normals
        float3 geoNormal = normalize( GetNormal( g_tHeightMap, i.TextureCoords, 40.0f ) );

        float3 vColor, vNormalTs, vRma;
        SplatmapMix( i.TextureCoords, vColor, vNormalTs, vRma );

        float3 vTangentUWs = normalize( cross( geoNormal, float3( 0, -1, 0 ) ) ); 
        float3 vTangentVWs = normalize( cross( geoNormal, -vTangentUWs ) );

        Material p = Material::Init();
        p.Albedo = vColor;
        p.Normal = TransformNormal( DecodeNormal( vNormalTs ), geoNormal, vTangentUWs, vTangentVWs );
        p.Roughness = vRma.r;
        p.Metalness = vRma.g;
        p.AmbientOcclusion = vRma.b;

        p.WorldPosition = i.WorldPosition;
        p.WorldPositionWithOffset = i.WorldPosition;
        p.ScreenPosition = i.ScreenPosition;
        p.GeometricNormal = geoNormal;

        p.WorldTangentU = vTangentUWs;
        p.WorldTangentV = vTangentVWs;

        // dyn combo
        if ( g_nDebugView != 0 )
        {
            return Debug( i, p );
        }

	    return ShadingModelStandard::Shade( p );
	}
}