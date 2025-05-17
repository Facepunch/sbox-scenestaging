FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    VrForward();
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

        if ( g_flBlendEnabled )
        {
            float h = Terrain::GetHeight( o.vPositionWs ) * 20000.0f; // !
            float dist = distance( h, o.vPositionWs.z );

            float blend = smoothstep( 0.0, 1.0, dist / g_flBlendLength );

            o.vPositionWs.z = lerp( h, o.vPositionWs.z, blend );
            o.vPositionPs.xyzw = Position3WsToPs( o.vPositionWs.xyz );            
        }

		return FinalizeVertex( o );
	}
}

PS
{
    #include "common/pixel.hlsl"

    void GetMaterial( float3 worldPos, float3 n, float diff, out float3 albedo, out float3 normal, out float roughness, out float ao, out float metal )
    {
        Texture2D tControlMap = Terrain::GetControlMap();
        float2 texSize = TextureDimensions2D( tControlMap, 0 ); // todo: store me in the struct...

        float2 uv = ( worldPos.xy ) / ( texSize * Terrain::Get().Resolution );
        float4 control = Tex2DS( Terrain::GetControlMap(), g_sBilinearBorder, uv );

        float2 texUV = worldPos.xy / 32;
        // texUV =  lerp(saturate( worldPos.xy - n.xy * diff ), texUV, saturate(n.z)); 

        float3 albedos[4], normals[4];
        float heights[4], roughnesses[4], aos[4], metalness[4];
        for ( int i = 0; i < 4; i++ )
        {
            float4 bcr = GetBindlessTexture2D( g_TerrainMaterials[ i ].bcr_texid ).Sample( g_sAnisotropic, texUV * g_TerrainMaterials[ i ].uvscale );
            float4 nho = GetBindlessTexture2D( g_TerrainMaterials[ i ].nho_texid ).Sample( g_sAnisotropic, texUV * g_TerrainMaterials[ i ].uvscale );

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

        albedo = albedos[0] * control.r + albedos[1] * control.g + albedos[2] * control.b + albedos[3] * control.a;
        normal = normals[0] * control.r + normals[1] * control.g + normals[2] * control.b + normals[3] * control.a; // additive?
        roughness = roughnesses[0] * control.r + roughnesses[1] * control.g + roughnesses[2] * control.b + roughnesses[3] * control.a;
        ao = aos[0] * control.r + aos[1] * control.g + aos[2] * control.b + aos[3] * control.a;
        metal = metalness[0] * control.r + metalness[1] * control.g + metalness[2] * control.b + metalness[3] * control.a;
    }

    void Terrain_MeshBlend( in out Material material )
    {
        if ( !g_flBlendEnabled )
            return;

        float h = Terrain::GetHeight( material.WorldPosition.xy ) * 20000.0f; // !

        float diff = ( material.WorldPosition.z - h ) / g_flBlendLength;
        float blend = 1 - saturate( diff );

        blend = pow( blend, 2 );

        float3 albedo;
        float3 normal;
        float roughness;
        float ao;
        float metal;
        GetMaterial( material.WorldPosition.xyz, material.GeometricNormal, diff, albedo, normal, roughness, ao, metal );

        // normal = TransformNormal( normal, material.GeometricNormal, i.vTangentUWs, i.vTangentVWs );

        material.Albedo = lerp( material.Albedo, albedo, blend );
        // material.Normal = lerp( material.Normal, normal, blend );
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