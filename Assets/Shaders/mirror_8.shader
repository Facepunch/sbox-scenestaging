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
    
	bool g_bReflection < Default(0.0f); Attribute( "HasReflectionTexture" ); > ;
	CreateTexture2D( g_ReflectionTexture ) < Attribute("ReflectionTexture");   SrgbRead( false ); Filter(MIN_MAG_MIP_LINEAR);    AddressU( CLAMP );     AddressV( CLAMP ); > ;    

	float4 SurfaceColor < UiType(Color); Default4(0.0, 0.5, 0.6, 0.5); UiGroup("Water"); > ;

	float4 MainPs( PixelInput i ) : SV_Target
	{
		float3 worldPos = g_vCameraPositionWs + i.vPositionWithOffsetWs;
		float3 op = worldPos;
		Material m = Material::From( i );
		float4 outCol = ShadingModelStandard::Shade( i, m );
		// 
		// Add reflection
		//
		if ( g_bReflection ) 
        {
            const float3 vRayWs = CalculateCameraToPositionDirWs( m.WorldPosition );

			float2 uv = i.vPositionSs.xy * g_vInvViewportSize; 

			float3 reflectColor = g_ReflectionTexture.SampleLevel(g_ReflectionTexture_sampler, uv, 0).rgb;

			float3 burned = pow( outCol.rgb * reflectColor, 0.66 );

			outCol.rgb = lerp(reflectColor, burned, 0.2);
        }


		outCol.rgb = Fog::Apply( worldPos, i.vPositionSs.xy, outCol.rgb );

		return outCol;
	}
}
