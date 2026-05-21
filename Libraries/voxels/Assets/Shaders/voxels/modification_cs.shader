MODES
{
    Default();
}

CS
{
    #include "Shaders/simplex3d.hlsl"

    struct Voxel
    {
        float Solidity;

        void Add(float distance)
        {
            Solidity = max(Solidity, saturate(-distance * 0.5 + 0.5));
        }

        void Subtract(float distance)
        {
            Solidity = min(Solidity, saturate(distance * 0.5 + 0.5));
        }
    };

    struct VoxelModificationEntry
    {
        uint ModificationTypeId;
        uint ParameterOffset;
    };

    enum
    {
        ModificationType_Plane = 0x01,
        ModificationType_Sphere = 0x02,
        ModificationType_WorldGen = 0x03
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

    struct VoxelColumn
    {
        uint2 Index;
        uint Count;

        float3 GetWorldPos(uint z)
        {
            return WorldOrigin + uint3(Index, z) * VoxelScale;
        }

        Voxel Get(uint z)
        {
            return GetVoxel(uint3(Index, z));
        }

        void Set(uint z, Voxel v)
        {
            SetVoxel(uint3(Index, z), v);
        }

        void Apply(uint z, uint operation, float worldDistance)
        {
            float distance = worldDistance * 0.5 / VoxelScale;

            if (distance > 1) return;

            Voxel v = Get(z);

            if (operation == 0) {
                v.Add(distance);
            } else {
                v.Subtract(distance);
            }

            Set(z, v);
        }
    };

    struct EmptyModification
    {
        void Apply(VoxelColumn c) { }
    };

    #include "Shaders/voxels/modifications/sphere.hlsl"
    #include "Shaders/voxels/modifications/plane.hlsl"
    #include "Shaders/voxels/modifications/worldgen.hlsl"

    [numthreads( 1, 1, 1 )]
    void MainCs(uint2 dispatchId: SV_DispatchThreadID)
    {
        for (int i = 0; i < ModificationCount; i++)
        {
            VoxelModificationEntry e = ModificationList[i];
            VoxelColumn c;

            c.Index = dispatchId;
            c.Count = VoxelCount.z;

            switch (e.ModificationTypeId)
            {
            case ModificationType_Plane:
                PlaneModification::Read(e.ParameterOffset).Apply(c);
                break;

            case ModificationType_Sphere:
                SphereModification::Read(e.ParameterOffset).Apply(c);
                break;

            case ModificationType_WorldGen:
                WorldGenModification::Read(e.ParameterOffset).Apply(c);
                break;
            }
        }
    }
}
