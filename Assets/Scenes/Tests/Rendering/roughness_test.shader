FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    VrForward();                                               // Indicates this shader will be used for main rendering
}

COMMON
{
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

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::From( i );
		m.Metalness = m.WorldPosition.z > 0 ? 0.0f : 1.0f;
		m.Roughness = sin( g_flTime) * 0.45 + 0.5f;
		return ShadingModelStandard::Shade( i, m );
	}
}
