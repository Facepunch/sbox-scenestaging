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
    uint3 VoxelSize < Attribute("VoxelSize"); > ;

    int3 WorldOrigin < Attribute("WorldOrigin"); > ;

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = voxel;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint2 dispatchId: SV_DispatchThreadID)
    {
        float2 worldPos2d = (WorldOrigin.xy + int2(dispatchId.xy)) / 128.0;

        float biome = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.25, 0.0), 2) * 0.5 + 0.625), 4);
        float caveyness = pow(saturate(FractalSimplexNoise3D(float3(worldPos2d * 0.25, 0.0), 2) * 4.0 - 1.0), 8);
        float baseHeight = FractalSimplexNoise3D(float3(worldPos2d, 0.0), 4);
        float height = lerp(baseHeight * 0.01 - 0.5, baseHeight * 0.25 + 0.75, biome);

        for (int z = 0; z < VoxelSize.z; z++)
        {
            int3 localPos = int3(dispatchId.xy, z);
            float3 worldPos = (WorldOrigin + localPos) / 128.0;

            if (height < worldPos.z) break;

            float density = FractalSimplexNoise3D(worldPos * float3(0.5, 0.5, 1.5), 6) + lerp(0.5, 0.125, caveyness);

            if (density < 0.0) continue;

            Voxel v;

            v.Value = uint(lerp(0, 255, saturate(density)));

            SetVoxel(VoxelOffset + localPos, v);
        }
    }
}
