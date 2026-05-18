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

    float3 WorldOrigin < Attribute("WorldOrigin"); > ;

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = voxel;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint2 dispatchId: SV_DispatchThreadID)
    {
        float2 worldPos2d = (WorldOrigin.xy + int2(dispatchId.xy) * VoxelScale) / 4096.0;
        float valueScale = 32.0 / VoxelScale;

        float detail1 = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d, 0.0), 6) * 0.5 + 0.5), 4);
        float detail2 = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d, 16.0), 6) * 0.5 + 0.5), 4);
        float detail3 = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d, 32.0), 6) * 0.5 + 0.5), 4);

        float biome1 = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.125, 0.0), 4) * 0.5 + 0.625), 4);
        float biome2 = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.25, 16.0), 4) * 0.5 + 0.625), 4);
        float biome3 = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.0625, 32.0), 4) * 0.5 + 0.625), 2);

        float surface = lerp(detail1 * 0.1 + 0.25, detail1 * 0.75 + 2.5, biome1);

        float cavernRoof = detail2 * 0.25 - 1.75 + biome3 * 2.0 + biome2;
        float cavernFloor = detail3 * 0.125 - 1.5 + biome3 * 2.0 - biome2 * 0.5;

        for (int z = 0; z < VoxelCount.z; z++)
        {
            int3 localPos = int3(dispatchId.xy, z);
            float3 worldPos = (WorldOrigin + localPos * VoxelScale) / 4096.0;
            float underground = (surface - worldPos.z) * 32.0 * valueScale;

            if (underground <= -1.0) break;

            float density = (FractalSimplexNoise3D(worldPos * float3(0.5, 0.5, 1.0), 6) + 0.55) * 16.0 * valueScale;

            if (density <= -1.0) continue;

            float cavern = max(cavernFloor - worldPos.z, worldPos.z - cavernRoof) * 32.0 * valueScale;

            if (cavern <= -1.0) continue;

            Voxel v;

            v.Value = uint(lerp(0, 255, saturate(min(cavern, min(underground, density)) * 0.5 + 0.5)));

            SetVoxel(VoxelOffset + localPos, v);
        }
    }
}
