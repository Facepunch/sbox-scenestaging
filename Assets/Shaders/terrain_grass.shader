//
// Simple Terrain shader with 4 layer splat
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
    Forward();
    Depth( S_MODE_DEPTH );
}

COMMON
{
    // Opt out of stupid shit
    #define CUSTOM_MATERIAL_INPUTS

    #include "common/shared.hlsl"
    #include "common/Bindless.hlsl"
    #include "terrain/TerrainCommon.hlsl"

    int g_nDebugView < Attribute( "DebugView" ); >;
    int g_nPreviewLayer < Attribute( "PreviewLayer" ); >;
}

struct VertexInput
{
	float3 PositionAndLod : POSITION < Semantic( PosXyz ); >;
};

struct PixelInput
{
    float3 LocalPosition : TEXCOORD0;
    float3 WorldPosition : TEXCOORD1;
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
    #include "terrain/TerrainClipmap.hlsl"

	PixelInput MainVs( VertexInput i )
	{
        PixelInput o;

        o.LocalPosition = Terrain_ClipmapSingleMesh( i.PositionAndLod, Terrain::GetHeightMap(), Terrain::Get().Resolution, Terrain::Get().TransformInv );

        o.LocalPosition.z *= Terrain::Get().HeightScale;

        o.WorldPosition = mul( Terrain::Get().Transform, float4( o.LocalPosition, 1.0 ) ).xyz;
        o.PixelPosition = Position3WsToPs( o.WorldPosition.xyz );
        o.LodLevel = i.PositionAndLod.z;

        // check for holes in vertex shader, better results and faster
        float hole = Terrain::GetHolesMap().Load( int3( o.LocalPosition.xy / Terrain::Get().Resolution, 0 ) ).r;
        if ( hole > 0.0f )
        {
            o.LocalPosition = float3( 0. / 0., 0, 0 );
            o.WorldPosition = mul( Terrain::Get().Transform, float4( o.LocalPosition, 1.0 ) ).xyz;
            o.PixelPosition = Position3WsToPs( o.WorldPosition.xyz );            
        }

		return o;
	}
}

//=========================================================================================================================

PS
{
    DynamicCombo( D_GRID, 0..1, Sys( ALL ) );    
    DynamicCombo( D_AUTO_SPLAT, 0..1, Sys( ALL ) );    

    #include "common/pixel.hlsl"
    #include "common/material.hlsl"
    #include "common/shadingmodel.hlsl"

    #include "terrain/TerrainNoTile.hlsl"

    float HeightBlend( float h1, float h2, float c1, float c2, out float ctrlHeight )
    {
        float h1Prefilter = h1 * sign( c1 );
        float h2Prefilter = h2 * sign( c2 );
        float height1 = h1Prefilter + c1;
        float height2 = h2Prefilter + c2;
        float blendFactor = (clamp(((height1 - height2) / ( 1.0f - Terrain::Get().HeightBlendSharpness )), -1, 1) + 1) / 2;
        ctrlHeight = c1 + c2;
        return blendFactor;
    }

    void Terrain_Splat4( in float2 texUV, in float4 control, out float3 albedo, out float3 normal, out float roughness, out float ao, out float metal, float distanceToCamera )
    {
        texUV /= 32;

        float3 albedos[4], normals[4];
        float heights[4], roughnesses[4], aos[4], metalness[4];
        for ( int i = 0; i < 4; i++ )
        {
            float4 bcr = Bindless::GetTexture2D( g_TerrainMaterials[ i ].bcr_texid ).Sample( g_sAnisotropic, texUV * g_TerrainMaterials[ i ].uvscale );
            float4 nho = Bindless::GetTexture2D( g_TerrainMaterials[ i ].nho_texid ).Sample( g_sAnisotropic, texUV * g_TerrainMaterials[ i ].uvscale );

            float3 normal = ComputeNormalFromRGTexture( nho.rg );
            normal.xz *= g_TerrainMaterials[ i ].normalstrength;
            normal = normalize( normal );

            albedos[i] = SrgbGammaToLinear( bcr.rgb );
            normals[i] = normal;
            roughnesses[i] = bcr.a;
            heights[i] = nho.b * g_TerrainMaterials[ i ].heightstrength;
            aos[i] = nho.a;
            metalness[i] = g_TerrainMaterials[ i ].metalness;
        }

        float ctrlHeight1, ctrlHeight2, ctrlHeight3;
        float blend01 = HeightBlend( heights[0], heights[1], control.r, control.g, ctrlHeight1 );
        float blend12 = HeightBlend( heights[1], heights[2], ctrlHeight1, control.b, ctrlHeight2 );
        float blend23 = HeightBlend( heights[2], heights[3], ctrlHeight2, control.a, ctrlHeight3 );

        if ( Terrain::Get().HeightBlending )
        {
            // Blend Textures based on calculated blend factors
            albedo = albedos[0] * blend01 + albedos[1] * (1 - blend01);
            albedo = albedo * blend12 + albedos[2] * (1 - blend12);
            albedo = albedo * blend23 + albedos[3] * (1 - blend23);

            normal = normals[0] * blend01 + normals[1] * (1 - blend01);
            normal = normal * blend12 + normals[2] * (1 - blend12);
            normal = normal * blend23 + normals[3] * (1 - blend23);        

            roughness = roughnesses[0] * blend01 + roughnesses[1] * (1 - blend01);
            roughness = roughness * blend12 + roughnesses[2] * (1 - blend12);
            roughness = roughness * blend23 + roughnesses[3] * (1 - blend23);          

            ao = aos[0] * blend01 + aos[1] * (1 - blend01);
            ao = ao * blend12 + aos[2] * (1 - blend12);
            ao = ao * blend23 + aos[3] * (1 - blend23);            

            metal = metalness[0] * blend01 + metalness[1] * (1 - blend01);
            metal = metal * blend12 + metalness[2] * (1 - blend12);
            metal = metal * blend23 + metalness[3] * (1 - blend23);            
        }
        else
        {
            albedo = albedos[0] * control.r + albedos[1] * control.g + albedos[2] * control.b + albedos[3] * control.a;
            normal = normals[0] * control.r + normals[1] * control.g + normals[2] * control.b + normals[3] * control.a; // additive?
            roughness = roughnesses[0] * control.r + roughnesses[1] * control.g + roughnesses[2] * control.b + roughnesses[3] * control.a;
            ao = aos[0] * control.r + aos[1] * control.g + aos[2] * control.b + aos[3] * control.a;
            metal = metalness[0] * control.r + metalness[1] * control.g + metalness[2] * control.b + metalness[3] * control.a;
        }

        
        // The only change to terrain shader, add AO to blend nicely with grass
        ao *= lerp( saturate( distanceToCamera / 2048.0f ), 1.0f, blend23 );
    }

	// 
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
        Texture2D tHeightMap = Bindless::GetTexture2D( Terrain::Get().HeightMapTexture );
        Texture2D tControlMap = Bindless::GetTexture2D( Terrain::Get().ControlMapTexture );

        float2 texSize = TextureDimensions2D( tHeightMap, 0 );
        float2 uv = i.LocalPosition.xy / ( texSize * Terrain::Get().Resolution );

        // Clip any of the clipmap that exceeds the heightmap bounds
        if ( uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0 )
        {
            clip( -1 );
            return float4( 0, 0, 0, 0 );
        }

        // calculate geometric normal
        float3 tangentU, tangentV;
        float3 geoNormal = Terrain_Normal( tHeightMap, uv, Terrain::Get().HeightScale, tangentU, tangentV );
        geoNormal = mul( Terrain::Get().Transform, float4( geoNormal, 0.0 ) ).xyz;

        float3 albedo = float3( 1, 1, 1 );
        float3 norm = float3( 0, 0, 1 );
        float roughness = 1;
        float ao = 1;
        float metalness = 0;

    #if D_GRID
        Terrain_ProcGrid( i.LocalPosition.xy, albedo, roughness );
    #else
        // Not adding up to 1 is invalid, but lets just give everything to the first layer
        float4 control = Terrain::GetControlMap().Sample( g_sBilinearBorder, uv );
        float sum = control.x + control.y + control.z + control.w;

        #if D_AUTO_SPLAT
        if ( sum != 1.0f )
        {
            float invsum = 1.0f - sum;
            float slope_weight = saturate( ( geoNormal.z - 0.99 ) * 100 );
            control.x += ( slope_weight ) * invsum;
            control.y += ( 1.0f - slope_weight ) * invsum;
        }
        #else
        // anything unsplatted, defualt to channel 0
        if ( sum != 1.0f ) { control.x += 1.0f - sum; }
        #endif

        float distanceToCamera = length( i.WorldPosition - g_vHighPrecisionLightingOffsetWs.xyz );
        Terrain_Splat4( i.LocalPosition.xy, control, albedo, norm, roughness, ao, metalness, distanceToCamera );
    #endif

        Material p = Material::Init();
        p.Albedo = albedo;
        p.Normal = TransformNormal( norm, geoNormal, tangentU, tangentV );
        p.Roughness = roughness;
        p.Metalness = metalness;
        p.AmbientOcclusion = ao;
        p.TextureCoords = uv;

        p.WorldPosition = i.WorldPosition;
        p.WorldPositionWithOffset = i.WorldPosition - g_vHighPrecisionLightingOffsetWs.xyz;
        p.ScreenPosition = i.ScreenPosition;
        p.GeometricNormal = geoNormal;

        p.WorldTangentU = tangentU;
        p.WorldTangentV = tangentV;

        if ( g_nDebugView != 0 )
        {
            // return Terrain_Debug( i.LodLevel, p.TextureCoords );
        }

	    return ShadingModelStandard::Shade( p );
	}
}