
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth(); 
	ToolsVis( S_MODE_TOOLS_VIS );
	ToolsWireframe( "vr_tools_wireframe.shader" );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float4 vScreenSpacePosition : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	float4 ScreenSpacePosition(float4 position)
	{
		float4 v = position * 0.5;
		v.xy += v.w;
		v.zw = position.zw;
		return v;
	}

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		PixelInput o = FinalizeVertex( i );
		o.vScreenSpacePosition = ScreenSpacePosition( i.vPositionPs );
		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;
	Texture2D g_tReflection < Attribute( "Reflection" ); SrgbRead( true ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float4 uv = i.vScreenSpacePosition;
		uv.xy *= -1.0;
		float4 reflectionColor = Tex2DS( g_tReflection, g_sSampler0, uv.xy / uv.w );

		float4 colour;
		colour.rgb = reflectionColor.rgb;
		colour.a = 1.0;

		return colour;
	}
}
