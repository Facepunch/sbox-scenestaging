MODES
{
    Default();
}

CS
{
    #include "Shaders/procgen/simplex3d.hlsl"

    struct Voxel
    {
        uint Value;
    };

    RWStructuredBuffer<Voxel> VoxelData < Attribute("VoxelData"); >;
    uint3 VoxelOffset < Attribute("VoxelOffset"); > ;
    uint2 VoxelStride < Attribute("VoxelStride"); > ;
    uint3 VoxelCount < Attribute("VoxelCount"); > ;
    float VoxelScale < Attribute("VoxelScale"); > ;

    int3 WorldOrigin < Attribute("WorldOrigin"); > ;

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = voxel;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint2 dispatchId: SV_DispatchThreadID)
    {
        float2 worldPos2d = (WorldOrigin.xy + int2(dispatchId.xy) * VoxelScale / 32.0) / 128.0;
        float valueScale = 32.0 / VoxelScale;

        float biome = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.25, 0.0), 2) * 0.5 + 0.625), 4);
        float caveyness = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.25, 0.0), 2) * 4.0 - 1.0), 4);
        float baseHeight = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d, 0.0), 4) * 0.5 + 0.5), 4);
        float height = lerp(baseHeight * 0.1 + 0.25, baseHeight * 0.75 + 1.5, biome);

        for (int z = 0; z < VoxelCount.z; z++)
        {
            int3 localPos = int3(dispatchId.xy, z);
            float3 worldPos = (WorldOrigin + localPos * VoxelScale / 32.0) / 128.0;
            float underground = (height - worldPos.z) * 32.0 * valueScale;

            if (underground <= -1.0) break;

            float density = (FractalSimplexNoise3D(worldPos * float3(0.5, 0.5, 1.0), 5) + lerp(1.25, 0.4, caveyness)) * 16.0 * valueScale;

            if (density <= -1.0) continue;

            Voxel v;

            v.Value = uint(lerp(0, 255, saturate(underground * 0.5 + 0.5) * saturate(density * 0.5 + 0.5)));

            SetVoxel(VoxelOffset + localPos, v);
        }
    }
}
