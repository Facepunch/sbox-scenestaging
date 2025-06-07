//
// this is a feasibility test, don't take this as final
//

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth();
}

COMMON
{
	#define S_SPECULAR 1
	#define F_DYNAMIC_REFLECTIONS 0
	#include "common/shared.hlsl"
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
		// Add your vertex manipulation functions here
		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"
    
	bool g_bRefraction < Default(0.0f); Attribute( "HasRefractionTexture" ); > ;
	CreateTexture2D( g_RefractionTexture ) < Attribute("RefractionTexture");   SrgbRead( false ); Filter(MIN_MAG_MIP_LINEAR);    AddressU( CLAMP );     AddressV( CLAMP ); > ;    
	float RefractionNormalScale < Default(0.5f); Range(0.01f, 1.0f); UiGroup("Refraction"); > ;
	

	bool g_bReflection < Default(0.0f); Attribute( "HasReflectionTexture" ); > ;
	CreateTexture2D( g_ReflectionTexture ) < Attribute("ReflectionTexture");   SrgbRead( false ); Filter(MIN_MAG_MIP_LINEAR);    AddressU( CLAMP );     AddressV( CLAMP ); > ;    
	float RelectionNormalScale < Default(0.5f); Range(0.01f, 1.0f); UiGroup("Reflection"); > ;

	float4 SurfaceColor < UiType(Color); Default4(0.0, 0.5, 0.6, 0.5); UiGroup("Water"); > ;

	float MaxDepth < Default(256.0f); Range(0.0f, 256); UiGroup("Water"); > ;
	float Thickness < Default(1.0f); Range(0.0f, 1); UiGroup("Water"); > ;
	float Refraction < Default(1.0f); Range(0.0f, 1); UiGroup("Water"); > ;
	float ReflectionRefraction < Default(1.0f); Range(0.0f, 1); UiGroup("Water"); > ;

	float BigWaveSize < Default(1.0f); Range(0.0f, 10); UiGroup("Big Wave"); > ;
	float BigWaveScale < Default(1.0f); Range(0.0f, 100); UiGroup("Big Wave"); > ;
	float BigWaveTime < Default(1.0f); Range(0.0f, 20); UiGroup("Big Wave"); > ;

	float SmallWaveSize < Default(0.4f); Range(0.0f, 10); UiGroup("Small Wave"); > ;
	float SmallWaveScale < Default(10.0f); Range(0.0f, 100); UiGroup("Small Wave"); > ;
	float SmallWaveTime < Default(13.0f); Range(0.0f, 20); UiGroup("Small Wave"); > ;

	float FlowSize < Default(1.0f); Range(0.0f, 20); UiGroup("Flow"); > ;
	float FlowSpeed < Default(0.01f); Range(0.0f, 1); UiGroup("Flow"); > ;

	float NormalScale < Default(0.5f); Range(0.0f, 2); UiGroup("Tweaks"); > ;
	float TextureScale < Default(128f); Range(0.0f, 1024); UiGroup("Tweaks"); > ;

	float g_fRoughness < Default(0.5f); Range(0.01f, 1.0f); UiGroup("Tweaks"); > ;
	float g_fMetalness < Default(0.5f); Range(0.01f, 1.0f); UiGroup("Tweaks"); > ;
	float g_fAmbientOcclusion < Default(0.5f); Range(0.01f, 1.0f); UiGroup("Tweaks"); > ;

	CreateInputTexture2D( EdgeFoam, Linear, 8, "", "", "Edge Foam", Default4( 0.5, 0.5, 1.0, 1 ) );
	CreateTexture2D( g_tEdgeFoam ) < Channel( RGBA, Box( EdgeFoam ), Linear ); OutputFormat( BC7 ); SrgbRead( false ); >;

	float4 MainPs( PixelInput i ) : SV_Target
	{
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;
		float distanceFromEye = length( i.vPositionWithOffsetWs );

		float3 op = worldPos;

		float textureScale = TextureScale;
		float moveSpeed = textureScale / 512;


		/// large bounce
		{
			worldPos.x += sin( (op.x / BigWaveScale) + (g_flTime * BigWaveTime) ) * BigWaveSize;
			worldPos.y += cos( (op.y / BigWaveScale) + (g_flTime * BigWaveTime) ) * BigWaveSize;
		}

		/// small bounce
		{
			worldPos.x += sin( (op.x / SmallWaveScale) + g_flTime * SmallWaveTime ) * SmallWaveSize;
			worldPos.y += cos( (op.y / SmallWaveScale) + g_flTime * SmallWaveTime ) * SmallWaveSize;
		}

		i.vTextureCoords.x = worldPos.x / textureScale;
		i.vTextureCoords.y = worldPos.y / textureScale; 

		i.vTextureCoords.x += sin( g_flTime * FlowSpeed / FlowSize ) * FlowSize * moveSpeed;
		i.vTextureCoords.y += cos( g_flTime * FlowSpeed / FlowSize ) * FlowSize * moveSpeed;



		Material m2 = Material::From( i );

		i.vTextureCoords.xy = i.vTextureCoords.xy * -1.1;
		i.vTextureCoords.x -= sin( g_flTime * moveSpeed * FlowSpeed / FlowSize ) * FlowSize * moveSpeed;
		i.vTextureCoords.y -= cos( g_flTime * moveSpeed * FlowSpeed / FlowSize ) * FlowSize * moveSpeed;

		//m = Material::lerp( m, m2, 0.5 );

		// i.vTextureCoords /= 3;

		Material m = Material::From( i );

		float3 camdir = CalculatePositionToCameraDirWs( i.vPositionWithOffsetWs.xyz + g_vHighPrecisionLightingOffsetWs.xyz );


		m = Material::lerp( m, m2, 0.5 );

		float3 normal = normalize( m.Normal );

		float normalSize = 0.05;
		float3 delta = op - worldPos;
		m.Normal.x += (delta.x * normalSize) - normalSize * 0.5f;
		m.Normal.y += (delta.y * normalSize) - normalSize * 0.5f;
		m.Normal = normalize( m.Normal );

		//
		// Adds a "distant" layer, which is larger to hide tiling
		//
		if ( false )
		{
			i.vTextureCoords *= 0.05;
			i.vTextureCoords.x += g_flTime * 0.01 * moveSpeed;
			Material mm = Material::From( i );

			float l = saturate( length( i.vPositionWithOffsetWs ) / 2048 );
			l = clamp( l, 0.0, 1 );
			l = pow( l, 1 );
			m = Material::lerp( m, mm, l );
		}

		// scale normals, artist adjustment
		{
			m.Normal = lerp( float3( 0, 0, 1 ), m.Normal, NormalScale );
		}

		// get flatter the further away
		
		{
			float lval = distanceFromEye / 2048;
			lval = clamp( lval, 0, 0.66 );

			m.Normal = normalize( lerp( m.Normal, float3( 0, 0, 1 ), lval ) ); 
		}

		
		m.Normal = normalize( lerp( float3( 0, 0, 1 ), m.Normal, 1 ) ); 
		m.Opacity = 1;
		m.Roughness = g_fRoughness;
		m.Metalness = g_fMetalness;
		m.AmbientOcclusion = g_fAmbientOcclusion;

		float3 worldNormal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		float fresnel = pow( 1.0 - dot( ( worldNormal ), camdir ), 5 );

		//
		// Add refraction
		//
		if ( g_bRefraction )
		{
			float colorSplit = 1;
			float2 uv = i.vPositionSs.xy * g_vInvViewportSize; 
			uv += -normal.xy * Refraction * 0.1 * RefractionNormalScale;

           	float3 col = Tex2DLevel( g_RefractionTexture, uv, 0 ).rgb;

			uv -= -normal.yx * Refraction * 0.1 * RefractionNormalScale * colorSplit;
			col.g = Tex2DLevel( g_RefractionTexture, uv, 0 ).g;
			uv += -normal.xy * Refraction * 0.1 * RefractionNormalScale * colorSplit;
			col.b = Tex2DLevel( g_RefractionTexture, uv, 0 ).b;
			m.Emission.rgb += col;
		}

		// 
		// Add reflection
		//
		if ( g_bReflection )
        {
            const float3 vRayWs = CalculateCameraToPositionDirWs( m.WorldPosition );

            float3 vReflectWs = reflect(vRayWs, normal);
            float2 vPositionSs = i.vPositionSs.xy;
			float2 uv = i.vPositionSs.xy * g_vInvViewportSize; 

			uv.x += normal.x * ReflectionRefraction * -0.1 * RelectionNormalScale; //only refract on x to avoid tear-away

			float3 col = g_ReflectionTexture.SampleLevel(g_ReflectionTexture_sampler, uv, 0).rgb;

			m.Emission.rgb += col * fresnel;
			//outCol.rgb += pow( col, 1 ) * fresnel;
        }

		float4 outCol = ShadingModelStandard::Shade( i, m );
		outCol.rgb = Fog::Apply( worldPos, i.vPositionSs.xy, outCol.rgb );

		return outCol;
	}
}
