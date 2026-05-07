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
    uint3 VoxelOffset < Attribute("VoxelOffset"); >;
    uint2 VoxelStride < Attribute("VoxelStride"); >;

    int3 WorldOrigin < Attribute("WorldOrigin"); > ;

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = voxel;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        float3 worldPos = (WorldOrigin + int3(dispatchId)) / 128.0;

        float biome = FractalSimplexNoise3D(float3(worldPos.xy / 4.0, 0.0), 2) * 0.5 + 0.625;
        float baseHeight = FractalSimplexNoise3D(float3(worldPos.xy, 0.0), 4);
        float height = lerp(baseHeight * 0.0625 + 0.25, baseHeight * 0.25 + 0.75, pow(biome, 4.0));
        float density = FractalSimplexNoise3D(worldPos * float3(1.0, 1.0, 2.0), 4) + 0.25;

        Voxel v;

        v.Value = height > worldPos.z && density > 0.0 ? 1 : 0;

        SetVoxel(VoxelOffset + dispatchId, v);
    }
}
