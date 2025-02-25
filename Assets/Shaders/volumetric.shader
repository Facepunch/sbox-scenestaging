FEATURES
{
    #include "common/features.hlsl"
}

MODES
{
    Forward();
    //Depth(S_MODE_DEPTH);
}

COMMON
{
    #include "common/shared.hlsl"
}

//------------------------------------------------------------------------------
// Input/Output Structures
//------------------------------------------------------------------------------

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

//------------------------------------------------------------------------------
// Vertex Shader
//------------------------------------------------------------------------------

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

//------------------------------------------------------------------------------
// Pixel Shader
//------------------------------------------------------------------------------

PS
{
    //------------------------------------------------------------------------------
    // Bullshit
    //------------------------------------------------------------------------------
    #define S_TRANSLUCENT 1
    
    //------------------------------------------------------------------------------
    // Includes
    //------------------------------------------------------------------------------
    #include "common/pixel.hlsl"
    #include "thirdparty/NanoVDB/VDBCommon.hlsli"
    
    //------------------------------------------------------------------------------
    // Resources & Constants
    //------------------------------------------------------------------------------
    
    //StaticCombo(S_MODE_DEPTH, 0..1, Sys(All));
    pnanovdb_buf_t GridBuffer < Attribute("GridBuffer"); > ;

    //------------------------------------------------------------------------------
    // Magic Numbers
    //------------------------------------------------------------------------------
    #define EARLY_EXIT_THRESHOLD 0.01f
    #define SHADOW_RAY_MIN_DISTANCE 5.0f
    #define SILVER_LINING_POWER 10.0f
    #define AMBIENT_OCCLUSION_SCALE 4.0f
    #define MULTI_SCATTER_SCALE 2.0f

    //------------------------------------------------------------------------------
    // Creates a shadow ray for light transmittance calculation
    //------------------------------------------------------------------------------
    VdbRay CreateShadowRay(float3 origin, float3 direction)
    {
        VdbRay shadowRay;
        shadowRay.Origin = origin;
        shadowRay.Direction = direction;
        shadowRay.TMin = SHADOW_RAY_MIN_DISTANCE;
        shadowRay.TMax = POSITIVE_INFINITY;
        return shadowRay;
    }

    //------------------------------------------------------------------------------
    // Calculates transmittance and lighting through the volume
    //------------------------------------------------------------------------------
    float4 GetTransmittanceLit(
        pnanovdb_vec3_t bbox_min,
        pnanovdb_vec3_t bbox_max,
        VdbRay ray,
        VdbSampler sampler,
        HeterogenousMedium medium,
        float3x4 ObjectToWorld,
        float3 WorldRay )
    {
        // Early exit if ray misses grid bounds
        if (!pnanovdb_hdda_ray_clip(bbox_min, bbox_max, ray.Origin, ray.TMin, ray.Direction, ray.TMax))
        {
            return 0.0f;
        }
    
        // Initialize HDDA traversal
        pnanovdb_hdda_t hdda;
        pnanovdb_hdda_init(hdda, ray.Origin, ray.TMin, ray.Direction, ray.TMax, 4);

        // Phase function parameters
        const float g1 = 0.4;    // Forward scattering
        const float g2 = -0.35;  // Backward scattering
        const float w = 0.8;     // Scattering weight blend

        float3 accumulatedLight = 0;
        float transmittance = 1.0f;

        // Get the first light as the sun
        DynamicLight light;
        light.Init(0, DynamicLightConstantByIndex(0));
        light.Direction = mul(float4(light.Direction, 0), ObjectToWorld);

        const float3 ambientLight = SampleEnvironmentMapLevel( -WorldRay, 1.0f, 0); // Global skybox
        const float3 atmosphereScatter = float3(0.5, 0.7, 1.0);

        // Main volume traversal loop
        while (pnanovdb_hdda_step(hdda))
        {
            // Sample current position
            pnanovdb_vec3_t posLocal = pnanovdb_hdda_ray_start(ray.Origin, hdda.tmin + 0.001, ray.Direction);
            pnanovdb_coord_t ijk = pnanovdb_hdda_pos_to_ijk(PNANOVDB_REF(posLocal));
            
            // Get current cell dimension
            int dim = pnanovdb_uint32_as_int32(
                pnanovdb_readaccessor_get_dim(
                    PNANOVDB_GRID_TYPE_FLOAT, 
                    sampler.GridBuffer, 
                    sampler.Accessor, 
                    PNANOVDB_REF(ijk)
                )
            );
    
            // Skip inactive or large cells
            pnanovdb_hdda_update(PNANOVDB_REF(hdda), ray.Origin, ray.Direction, dim);
            if (hdda.dim > 1 || !pnanovdb_readaccessor_is_active(sampler.GridType, sampler.GridBuffer, sampler.Accessor, PNANOVDB_REF(ijk)))
            {
                continue;
            }
    
            // Calculate density and step size
            float density = ReadValue(posLocal, sampler.GridBuffer, sampler.GridType, sampler.Accessor) * medium.densityScale;
            float dt = pnanovdb_grid_get_voxel_size(sampler.GridBuffer, sampler.Grid, hdda.dim) * medium.densityScale;

            if (density > 0.0f)
            {
                // Calculate phase function
                float cosTheta = dot(light.Direction, -ray.Direction);
                float phase = lerp(
                    HenyeyGreenstein(cosTheta, g1),
                    HenyeyGreenstein(cosTheta, g2),
                    w
                );

                // Calculate lighting effects
                float viewDotSun = dot(ray.Direction, light.Direction);
                float silverLining = pow(max(0, viewDotSun), SILVER_LINING_POWER) * 2.0 * density;
                float ambientOcclusion = exp(-density * AMBIENT_OCCLUSION_SCALE);

                // Calculate shadow contribution
                // Don't use HDDA for this, it can be apprioximated linear raymarching
                // This is where we plug the TAA
                float shadowTransmittance = GetTransmittance(
                    bbox_min,
                    bbox_max,
                    CreateShadowRay(posLocal, light.Direction),
                    sampler,
                    medium,
                    8.0f
                );

                // Calculate multiple scattering
                float powder = 1.0 - exp(-density * MULTI_SCATTER_SCALE);
                float3 multiScatter = powder * ambientLight * ambientOcclusion;

                // Accumulate lighting
                float extinction = exp(-density * dt);
                float3 directLight = light.Color * phase * (shadowTransmittance + silverLining);
                float3 indirectLight = multiScatter;

                accumulatedLight += (directLight + indirectLight) * density * transmittance * dt;
            }

            // Update transmittance
            transmittance *= exp(-density * dt);

            // Early exit on full opacity
            if (transmittance < EARLY_EXIT_THRESHOLD)
            {
                break;
            }
        }

        // Add atmospheric scattering
        float3 finalColor = accumulatedLight + atmosphereScatter * 0.2 * transmittance;
        return float4(finalColor * 5, 1.0 - transmittance);
    }

    float4 MainPs(PixelInput input) : SV_Target0
    {
        // Initialize volume sampling
        VdbSampler sampler = InitVdbSampler(GridBuffer);
        float3 bboxMin = pnanovdb_root_get_bbox_min(sampler.GridBuffer, sampler.Root);
        float3 bboxMax = pnanovdb_root_get_bbox_max(sampler.GridBuffer, sampler.Root);

        // Calculate ray parameters
        bool isOrtho = g_vInvProjRow3.z == 0.0f;
        float3 worldPosition = input.vPositionWithOffsetWs.xyz + g_vCameraPositionWs;
        float3 worldRay = isOrtho ? g_vCameraDirWs : normalize(input.vPositionWithOffsetWs.xyz);
        float3 localRay = mul(float4(worldRay, 0), input.ObjectToWorld).xyz;
        
        // Create primary ray
        VdbRay ray = PrepareRayFromPixel(
            sampler.GridBuffer, 
            sampler.Grid, 
            input.LocalPosition, 
            input.LocalPosition + (localRay * 1000.0f)
        );

        // Configure medium properties
        HeterogenousMedium medium;
        medium.densityScale = 1.0f;
        medium.densityMin = 0.0f;
        medium.densityMax = 1.0f;
        medium.anisotropy = 0.0f;
        medium.albedo = 1;

        // Calculate final color
        RandomSequence randSequence;
        randSequence.state = 0; // TODO: Implement TAA integration

        float4 result = GetTransmittanceLit(
            bboxMin,
            bboxMax,
            ray,
            sampler,
            medium,
            input.ObjectToWorld,
            worldRay
        );
        
        // Discard fully transparent pixels
        if (result.w < 0.01f)
        {
            discard;
        }
        
        return result;
    }
}