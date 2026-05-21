MODES
{
    Default();
}

CS
{
    struct Voxel
    {
        float Solidity;
    };

    struct VoxelModificationEntry
    {
        uint ModificationTypeId;
        uint ParameterOffset;
    };

    enum
    {
        ModificationType_Sphere = 1
    };

    struct EmptyModification
    {
        Voxel Apply(Voxel voxel, float3 worldPos)
        {
            return voxel;
        }
    };

    RWStructuredBuffer<uint> VoxelData < Attribute("VoxelData"); >;
    uint3 VoxelOffset < Attribute("VoxelOffset"); >;
    uint3 VoxelStride < Attribute("VoxelStride"); >;
    uint3 VoxelCount < Attribute("VoxelCount"); >;

    float VoxelScale < Attribute("VoxelScale"); > ;
    float3 WorldOrigin < Attribute("WorldOrigin"); >;

    uint ModificationCount < Attribute("ModificationCount"); > ;
    StructuredBuffer<VoxelModificationEntry> ModificationList < Attribute("ModificationList"); >;
    StructuredBuffer<uint> ParameterData < Attribute("ParameterData"); > ;

    #include "Shaders/voxels/modifications/sphere.hlsl"

    void SetVoxel(uint3 index, Voxel voxel)
    {
        VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z] = uint(saturate(voxel.Solidity) * 255);
    }

    Voxel GetVoxel(uint3 index)
    {
        uint rawValue = VoxelData[index.x + VoxelStride.x * index.y + VoxelStride.y * index.z];

        Voxel v;

        v.Solidity = saturate(rawValue / 255.0);

        return v;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint2 dispatchId: SV_DispatchThreadID)
    {
        for (uint z = 0; z < VoxelCount.z; z++)
        {
            uint3 voxelIndex = uint3(dispatchId, z);
            float3 worldPos = WorldOrigin + voxelIndex * VoxelScale;
            Voxel v = GetVoxel(voxelIndex);

            for (int i = 0; i < ModificationCount; i++)
            {
                VoxelModificationEntry e = ModificationList[i];

                switch (e.ModificationTypeId)
                {
                case ModificationType_Sphere:
                    v = SphereModification::Read(e.ParameterOffset).Apply(v, worldPos);
                    break;
                }
            }
            
            SetVoxel(voxelIndex, v);
        }
    }
}
