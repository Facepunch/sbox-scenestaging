FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
	VrForward();
	ToolsVis( S_MODE_TOOLS_VIS );
}

COMMON
{
	#define S_TRANSLUCENT 1
	#define F_DYNAMIC_REFLECTIONS 1
	#define S_SPECULAR 1
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

    RenderState( BlendEnable, true );
    RenderState( SrcBlend, SRC_ALPHA );
    RenderState( DstBlend, INV_SRC_ALPHA );
    RenderState( BlendOp, ADD );
    RenderState( SrcBlendAlpha, ONE );
    RenderState( DstBlendAlpha, INV_SRC_ALPHA );
    RenderState( BlendOpAlpha, ADD );

	BoolAttribute( bWantsFBCopyTexture, true );
	BoolAttribute( UsesDynamicReflections, true );



	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );
		/* m.Metalness = 1.0f; // Forces the object to be metalic */

		m.Opacity = 0.5;
		return ShadingModelStandard::Shade( i, m );
	}
}
