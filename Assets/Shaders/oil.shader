HEADER
{
	Description = "Template Shader for S&box";
}

MODES
{
    VrForward();
	Depth( S_MODE_DEPTH ); 
	Reflection( S_MODE_REFLECTIONS );
    ToolsVis( S_MODE_TOOLS_VIS );
    ToolsWireframe( S_MODE_TOOLS_WIREFRAME );
}

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
	#include "common/shared.hlsl"

	StaticCombo( S_MODE_DEPTH, 0..1, Sys(All ) );
    StaticCombo( S_MODE_REFLECTIONS, 0..1, Sys(All ) );
    StaticCombo( S_MODE_TOOLS_WIREFRAME, 0..1, Sys( ALL ) );

    StaticComboRule( Allow1( S_MODE_DEPTH, S_MODE_REFLECTIONS ) );
    StaticComboRule( Allow1( S_MODE_DEPTH, S_MODE_TOOLS_WIREFRAME ) );
    StaticComboRule( Allow1( S_MODE_REFLECTIONS, S_MODE_TOOLS_WIREFRAME ) );

	DynamicCombo( D_FLUID_SIMULATION, 0..1, Sys( All ) );

	#define F_DYNAMIC_REFLECTIONS 1

    // Fluid shit
    Texture2DArray FluidTexture < Attribute("FluidTexture"); > ;
    float4x4 PositionToBounds < Attribute("PositionToBounds"); > ;
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

		// FinalizeVertex should process what's on vPositionWs to
		o.vPositionWs.xy += g_vCameraPositionWs.xy;
		o.vPositionWs.xy += g_vCameraDirWs.xy * 96.0f; // Roughly the distance from camera to player

		// Fluid sim vertice stuff
		#if D_FLUID_SIMULATION
			int3 dims;
			FluidTexture.GetDimensions(dims.x, dims.y, dims.z);

            float2 uv = mul(PositionToBounds, float4(-o.vPositionWs.xyz, 1.0f) ).xy;

            float pressure = FluidTexture.SampleLevel(g_sBilinearClamp, float3(uv, 0), 0).b;
            o.vPositionWs.z -= pressure * 0.5f;
		#endif

		o.vPositionPs = Position3WsToPs( o.vPositionWs );

		o = FinalizeVertex( o );
		return o;
	}
}

//=========================================================================================================================

PS
{
    #include "common/pixel.hlsl"
	#include "raytracing/reflections.hlsl"

	// This is stupid and nonstandard, I want to get rid of it
    #if ( S_MODE_REFLECTIONS )
		#define FinalOutput ReflectionOutput
        #define Target
	#else
		#define FinalOutput float4
        #define Target : SV_Target0
	#endif

	#if ( S_MODE_TOOLS_WIREFRAME )
		RenderState( FillMode, WIREFRAME );
		RenderState( SlopeScaleDepthBias, 0.5 ); // Depth bias params tuned for plantation_source2 under DX11
		RenderState( DepthBiasClamp, 0.0005 );
	#endif

	class OilMaterial
    {
        static float3 CalculateWorldSpaceNormal(float2 uv, float2 texelSize)
        {
            // Sample the texture at the neighboring texels
            float left = FluidTexture.SampleLevel(g_sBilinearClamp, float3( uv - float2(texelSize.x, 0), 0 ), 0).b;
            float right = FluidTexture.SampleLevel(g_sBilinearClamp, float3( uv + float2(texelSize.x, 0), 0 ), 0).b;
            float top = FluidTexture.SampleLevel(g_sBilinearClamp, float3( uv + float2(0, texelSize.y), 0 ), 0).b;
            float bottom = FluidTexture.SampleLevel(g_sBilinearClamp, float3( uv - float2(0, texelSize.y), 0 ), 0).b;

            // Calculate deltas
            float deltaX = right - left;
            float deltaY = top - bottom;

            // Construct the normal
            float3 normal;
            normal.x = deltaX * 0.5f;
            normal.y = deltaY * 0.5f;
            normal.z = 1.0f;

            // Normalize the normal
            normal = normalize(normal);

            return normal;
        }

        static Material Get( PixelInput i )
		{
			Material m = Material::From(i);
			m.Roughness = 0.15f;
			m.Albedo = 0.004f;
			m.Normal = float3(0,0,1);
			m.Metalness = 0.0f;
			m.AmbientOcclusion = 1.0f;

			// Seems backwards.. But it's what Valve were doing?
			m.WorldPosition = i.vPositionWithOffsetWs + g_vHighPrecisionLightingOffsetWs.xyz;
			m.WorldPositionWithOffset = i.vPositionWithOffsetWs;
			m.ScreenPosition = i.vPositionSs;

			#if D_FLUID_SIMULATION
				int3 dims;
				FluidTexture.GetDimensions(dims.x, dims.y, dims.z);

				float2 uv = mul(PositionToBounds, float4(-m.WorldPosition, 1.0f) ).xy;

                m.Normal = CalculateWorldSpaceNormal(uv, 1.0/512.0f );

                float flFoam = saturate( FluidTexture.Sample(g_sBilinearClamp, float3(uv, 1)).b );
                m.Albedo = lerp(m.Albedo, 1.0f, flFoam);
                m.Roughness = lerp(m.Roughness, 1.0f, flFoam);
				
			#endif

			return m;
		}
	};

	FinalOutput MainPs( PixelInput i ) Target
	{
		#if ( S_MODE_TOOLS_WIREFRAME )
            return 1;
        #endif

		Material m = OilMaterial::Get( i );
		
		#if S_MODE_REFLECTIONS
            return Reflections::From( i, m, 10 );
        #else
			return ShadingModelStandard::Shade( i, m );
		#endif
	}
}
