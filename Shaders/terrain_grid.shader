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

        float chessboard = CheckersGradBox( i.WorldPosition.xy / 64 );

        Material p = Material::Init();
        p.Albedo = 0.4 + chessboard * float3(0.2, 0.2, 0.2);
        p.Normal = geoNormal;
        p.Roughness = 0.5f - chessboard * 0.2f;
        p.Metalness = 0.0f;
        p.AmbientOcclusion = 1.0f;
        p.TextureCoords = uv;

        p.WorldPosition = i.WorldPosition;
        p.WorldPositionWithOffset = i.WorldPosition;
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