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

    float3 WorldOrigin < Attribute("WorldOrigin"); > ;

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = voxel;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        Voxel v;

        float noise = 0.0;

        float3 worldPos = WorldOrigin + dispatchId / 128.0;

        float height = FractalSimplexNoise3D(float3(worldPos.xy, 0.0), 4) * 0.25 + 0.75;
        float density = FractalSimplexNoise3D(worldPos * float3(1.0, 1.0, 2.0), 4) + 0.25;

        v.Value = height > worldPos.z && density > 0.0 ? 1 : 0;

        SetVoxel(VoxelOffset + dispatchId, v);
    }
}
