// Ideally you wouldn't need half these includes for an unlit shader
// But it's stupiod

FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    Depth( S_MODE_DEPTH );
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
    float3 LocalPosition : TEXCOORD12;
    float3x4 ObjectToWorld : TEXCOORD13;
};


VS
{
    #include "common/vertex.hlsl"

    PixelInput MainVs(VertexInput input)
    {
        PixelInput output = ProcessVertex(input);
        output.LocalPosition = input.vPositionOs;
        output.ObjectToWorld = CalculateInstancingObjectToWorldMatrix(input);
        return FinalizeVertex(output);
    }
}

PS
{
    //
    // Includes
    //
    #include "common/pixel.hlsl"
    #include "thirdparty/NanoVDB/VDBCommon.hlsli"
    
    //
    // Render States & Combos
    //
    //RenderState(BlendEnable, true);
    //RenderState(SrcBlend, SRC_ALPHA);
    //RenderState(DstBlend, INV_SRC_ALPHA);

    StaticCombo(S_MODE_DEPTH, 0..1, Sys(All));

    //
    // Resources
    //
    pnanovdb_buf_t GridBuffer < Attribute("GridBuffer"); > ;

    //
    // Helpers
    //
    float HenyeyGreensteinPhase(float cosTheta, float g)
    {
        float g2 = g * g;
        return (1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5);
    }

    float3 CalculateCloudColor(float transmittance, float phase, HeterogenousMedium medium)
    {
        float attenuation = exp(-medium.densityScale * transmittance);
        float3 cloudColor = float3(1.0, 1.0, 1.0) * attenuation * phase;
        return cloudColor * transmittance;
    }

    //
    // Main Pixel Shader
    //
    float4 MainPs(PixelInput input) : SV_Target0
    {
        // Initialize VDB sampling
        VdbSampler sampler = InitVdbSampler(GridBuffer);
        float3 bboxMin = pnanovdb_root_get_bbox_min(sampler.GridBuffer, sampler.Root);
        float3 bboxMax = pnanovdb_root_get_bbox_max(sampler.GridBuffer, sampler.Root);

        // Calculate ray parameters
        float3 worldPosition = input.vPositionWithOffsetWs.xyz + g_vCameraPositionWs;
        float3 worldRay = normalize(input.vPositionWithOffsetWs.xyz);
        float3 localRay = mul(float4(worldRay, 0), input.ObjectToWorld).xyz;
        
        // Setup ray marching
        VdbRay ray = PrepareRayFromPixel(
            sampler.GridBuffer, 
            sampler.Grid, 
            input.LocalPosition, 
            input.LocalPosition + (localRay * 1000.0f)
        );

        // Setup medium properties
        HeterogenousMedium medium;
        medium.densityScale = 0.2f;
        medium.densityMin = 0.0f;
        medium.densityMax = 1.0f;
        medium.anisotropy = 0.0f;
        medium.albedo = 1.0f;

        // Calculate lighting
        RandomSequence randSequence;
        randSequence.state = 1234;
        
        float transmittance = 1.0 - GetTransmittance(
            bboxMin, 
            bboxMax, 
            ray, 
            GridBuffer, 
            sampler.GridType, 
            sampler.Accessor, 
            medium, 
            1.0f, 
            randSequence
        );

        // Calculate phase function
        float3 sunDir = normalize(float3(0.0, -1.0, 0.0));
        float cosTheta = saturate(dot(normalize(-localRay), sunDir));
        float phase = HenyeyGreensteinPhase(cosTheta, medium.anisotropy);

        // Calculate final color
        float3 cloudColor = CalculateCloudColor(transmittance, phase, medium);

#if S_MODE_DEPTH == 1
        OpaqueFadeDepth(transmittance, input.vPositionSs.xy);
#endif

        return float4(cloudColor, transmittance);
    }
}
