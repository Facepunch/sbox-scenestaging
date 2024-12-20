// Ideally you wouldn't need half these includes for an unlit shader
// But it's stupiod

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
	#include "common/shared.hlsl"
}

MODES
{
	VrForward();
	Depth();
	ToolsVis( S_MODE_TOOLS_VIS );
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

	float4 MainPs( PixelInput pi ) : SV_Target0
	{
		Material m = Material::From( pi);
		m.Normal = pi.vNormalWs;
		
		uint nTest = -m.WorldPosition.x / 96;
		
		float3 vColor = 0;

		if( nTest == 0 )
		{
			return float4( Fog::Apply( m.WorldPosition, m.ScreenPosition.xy, 0.0f ), 1.0f );
		}
		else if( nTest == 1 )
		{
			for (uint index = 0; index < DynamicLight::Count(m.ScreenPosition.xy); index++)
			{
				Light light = DynamicLight::From(m.ScreenPosition.xy, m.WorldPosition, index);
				vColor += light.Color * light.Attenuation * max( dot(light.Direction, m.Normal), 0 );
			}
			return float4( vColor, 1);
		}
		else if( nTest == 2 )
		{
			for (uint index = 0; index < Light::Count(m.ScreenPosition.xy); index++)
			{
				Light light = Light::From(m.ScreenPosition.xy, m.WorldPosition, index);
				vColor += light.Color * light.Attenuation * max( dot(light.Direction, m.Normal), 0 );
			}
			return float4( vColor, 1);
		}
		else if( nTest == 3 )
		{
			return float4( AmbientLight::From( m.WorldPosition, m.ScreenPosition.xy, m.Normal ), 1.0f );
		}
		else if( nTest == 4 )
		{
			return float4( 1, 1, 0, 1 );
		}
		else if( nTest == 5 )
		{
			return float4( 1, 0, 1, 1 );
		}
		else if( nTest == 6 )
		{
			return float4( 0, 1, 1, 1 );
		}
		else if( nTest == 7 )
		{
			return float4( 1, 1, 1, 1 );
		}
		else

		return 0;
	}
}
