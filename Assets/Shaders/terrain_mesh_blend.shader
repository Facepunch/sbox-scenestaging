HEADER
{
    Description = "Terrain Mesh Blend";
}

FEATURES
{
    #include "common/features.hlsl"
    Feature( F_ALPHA_TEST, 0..1, "Rendering" );
}

MODES
{
    Forward();
    Depth( S_MODE_DEPTH );
    ToolsShadingComplexity( "vr_tools_shading_complexity.shader" );
}

COMMON
{
    #include "common/shared.hlsl"
    #include "terrain/TerrainCommon.hlsl"

    bool g_flBlendEnabled < Default( 1 ); UiGroup( "Terrain Blending"); >;
    float g_flBlendLength < Default( 25f ); Range( 0.0f, 100.0f ); UiGroup( "Terrain Blending"); >;
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );

        if ( g_flBlendEnabled && Terrain::IsInBounds( o.vPositionWs.xyz ) )
        {
            float h = Terrain::GetWorldHeight( o.vPositionWs.xyz );
            float blend = smoothstep( 0.0, 1.0, abs( o.vPositionWs.z - h ) / g_flBlendLength );

            o.vPositionWs.z = lerp( h, o.vPositionWs.z, blend );
            o.vPositionPs.xyzw = Position3WsToPs( o.vPositionWs.xyz );
        }

		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/utils/Material.CommonInputs.hlsl"
    #include "common/pixel.hlsl"

    StaticCombo( S_ALPHA_TEST, F_ALPHA_TEST, Sys( ALL ) );

    void SampleTerrainMaterial( TerrainMaterial mat, float2 texUV, out float3 albedo, out float3 normal, out float roughness, out float height, out float ao, out float metal )
    {
        float4 bcr = GetBindlessTexture2D( mat.bcr_texid ).Sample( g_sAnisotropic, texUV * mat.uvscale );
        float4 nho = GetBindlessTexture2D( mat.nho_texid ).Sample( g_sAnisotropic, texUV * mat.uvscale );

        normal = ComputeNormalFromRGTexture( nho.rg );
        normal.xz *= mat.normalstrength;
        normal = normalize( normal );

        albedo = SrgbGammaToLinear( bcr.rgb );
        roughness = bcr.a;
        height = nho.b * mat.heightstrength;
        ao = nho.a;
        metal = mat.metalness;
    }

    void GetMaterial( float3 worldPos, float3 worldNormal, float diff, out float3 albedo, out float3 normal, out float roughness, out float ao, out float metal )
    {
        float3 localPos = Terrain::WorldToLocal( worldPos );

        Texture2D tControlMap = Terrain::GetControlMap();
        float2 texSize = TextureDimensions2D( tControlMap, 0 );

        float2 uv = localPos.xy / ( texSize * Terrain::Get().Resolution );

        CompactTerrainMaterial controlData = CompactTerrainMaterial::DecodeFromFloat( tControlMap.SampleLevel( g_sPointClamp, uv, 0 ).r );

        float2 texUV = localPos.xy / 32.0;

        // Sample base material
        TerrainMaterial baseMat = g_TerrainMaterials[controlData.BaseTextureId];

        float3 baseNormal;
        float baseHeight;
        SampleTerrainMaterial( baseMat, texUV, albedo, baseNormal, roughness, baseHeight, ao, metal );
        normal = baseNormal;

        // Blend with overlay material
        float materialBlend = controlData.GetNormalizedBlend();
        TerrainMaterial overlayMat = g_TerrainMaterials[controlData.OverlayTextureId];

        if ( materialBlend > 0.01 && overlayMat.bcr_texid > 0 )
        {
            float3 overlayAlbedo, overlayNormal;
            float overlayRoughness, overlayHeight, overlayAo, overlayMetal;
            SampleTerrainMaterial( overlayMat, texUV, overlayAlbedo, overlayNormal, overlayRoughness, overlayHeight, overlayAo, overlayMetal );

            // Height blending if enabled
            float blend = materialBlend;
            if ( Terrain::Get().HeightBlending )
            {
                float sharpness = Terrain::Get().HeightBlendSharpness;
                float ha = baseHeight + ( 1.0 - materialBlend );
                float hb = overlayHeight + materialBlend;
                float maxH = max( ha, hb ) - ( 1.0 / sharpness );
                float wa = max( ha - maxH, 0 );
                float wb = max( hb - maxH, 0 );
                blend = wb / max( wa + wb, 0.001 );
            }

            albedo = lerp( albedo, overlayAlbedo, blend );
            normal = normalize( lerp( normal, overlayNormal, blend ) );
            roughness = lerp( roughness, overlayRoughness, blend );
            ao = lerp( ao, overlayAo, blend );
            metal = lerp( metal, overlayMetal, blend );
        }
    }

    void Terrain_MeshBlend( in out Material material )
    {
        if ( !g_flBlendEnabled || !Terrain::IsInBounds( material.WorldPosition ) )
            return;

        float blend = Terrain::GetBlendFactor( material.WorldPosition, g_flBlendLength );
        blend = pow( blend, 2 );

        float diff = Terrain::GetDistanceToSurface( material.WorldPosition ) / g_flBlendLength;

        float3 albedo;
        float3 normal;
        float roughness;
        float ao;
        float metal;
        GetMaterial( material.WorldPosition.xyz, material.GeometricNormal, diff, albedo, normal, roughness, ao, metal );

        material.Albedo = lerp( material.Albedo, albedo, blend );
        material.Normal = normalize( lerp( material.Normal, normal, blend ) );
        material.Roughness = lerp( material.Roughness, roughness, blend );
        material.AmbientOcclusion = lerp( material.AmbientOcclusion, ao, blend );
        material.Metalness = lerp( material.Metalness, metal, blend );
    }

	float4 MainPs( PixelInput i ) : SV_Target0
	{
        Material m = Material::From( i );

        Terrain_MeshBlend( m );

	    return ShadingModelStandard::Shade( i, m );
	}
}
