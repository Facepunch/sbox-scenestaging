//=========================================================================================================================
// Optional
//=========================================================================================================================
HEADER
{
	CompileTargets = ( IS_SM_50 && ( PC || VULKAN ) );
	Description = "Shader for geometry without texture coordinates";
}

//=========================================================================================================================
// Optional
//=========================================================================================================================
FEATURES
{
    #include "common/features.hlsl"
}

//=========================================================================================================================
COMMON
{
	#include "common/shared.hlsl"
}

//=========================================================================================================================

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

//=========================================================================================================================

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	
	float3 vPositionOs : TEXCOORD15;
	float3 vNormalOs : TEXCOORD16;
};

//=========================================================================================================================

VS
{
	#include "common/vertex.hlsl"

	//
	// Main
	//
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		
		o.vPositionOs = i.vPositionOs;
		o.vNormalOs = i.vNormalOs.xyz;

		return FinalizeVertex( o );
	}
}

//=========================================================================================================================

PS
{
	#define TRIPLANAR_OBJECT_SPACE
	
	#include "sdf3d/triplanar.hlsl"

	//
	// Main
	//
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = ToMaterialTriplanar( i, g_tColor, g_tNormal, g_tRma );

		return ShadingModelStandard::Shade( i, m );
	}
}
