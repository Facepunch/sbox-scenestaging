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

		// 
		// Add reflection
		//
		if ( g_bReflection ) 
        {
            const float3 vRayWs = CalculateCameraToPositionDirWs( m.WorldPosition );

			float2 uv = i.vPositionSs.xy * g_vInvViewportSize; 

			uv.x = 1 - uv.x;

			float3 col = g_ReflectionTexture.SampleLevel(g_ReflectionTexture_sampler, uv, 0).rgb;

			m.Emission.rgb += col;
        }

		float4 outCol = ShadingModelStandard::Shade( i, m );
		outCol.rgb = Fog::Apply( worldPos, i.vPositionSs.xy, outCol.rgb );

		return outCol;
	}
}
