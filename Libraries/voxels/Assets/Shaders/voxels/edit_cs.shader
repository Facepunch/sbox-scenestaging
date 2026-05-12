MODES
{
    Default();
}

CS
{
    struct Voxel
    {
        uint Value;
    };

    RWStructuredBuffer<Voxel> VoxelData < Attribute("VoxelData"); >;
    uint3 VoxelOffset < Attribute("VoxelOffset"); >;
    uint2 VoxelStride < Attribute("VoxelStride"); >;
    uint3 VoxelSize < Attribute("VoxelSize"); >;

    float3 EditOriginA < Attribute("EditOriginA"); > ;
    float3 EditOriginB < Attribute("EditOriginB"); > ;
    float EditRadius < Attribute("EditRadius"); > ;

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = voxel;
    }

    Voxel GetVoxel(uint3 index)
    {
        return VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z];
    }

    float GetEditDistance(float3 pos)
    {
        float3 diff = EditOriginB - EditOriginA;
        float capsuleLengthSq = dot(diff, diff);
        float along = saturate(dot(pos - EditOriginA, diff) / capsuleLengthSq);
        float3 closest = lerp(EditOriginA, EditOriginB, along);

        return length(pos - closest) - EditRadius;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId: SV_DispatchThreadID)
    {
        int3 localPos = int3(dispatchId);
        float distance = GetEditDistance(localPos);

        if (distance < 1.0)
        {
            Voxel v = GetVoxel(VoxelOffset + dispatchId);

            v.Value = min(v.Value, uint(saturate(0.5 + distance * 0.5) * 255));

            SetVoxel(VoxelOffset + dispatchId, v);
        }
    }
}
