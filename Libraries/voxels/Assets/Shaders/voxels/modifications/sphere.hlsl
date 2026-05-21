struct SphereModification
{
    uint Operation;
    float3 WorldOrigin;
    float WorldRadius;

    Voxel Apply(Voxel voxel, float3 worldPos)
    {
        float distance = (length(worldPos - WorldOrigin) - WorldRadius) / VoxelScale;

        if (Operation == 0) {
            voxel.Solidity = max(voxel.Solidity, saturate(-distance * 0.5 + 0.5));
        } else {
            voxel.Solidity = min(voxel.Solidity, saturate(distance * 0.5 + 0.5));
        }

        return voxel;
    }

    static SphereModification Read(uint offset)
    {
        SphereModification mod;

        mod.Operation = ParameterData[offset + 0];
        mod.WorldOrigin = float3(
            asfloat(ParameterData[offset + 1]),
            asfloat(ParameterData[offset + 2]),
            asfloat(ParameterData[offset + 3]));
        mod.WorldRadius = asfloat(ParameterData[offset + 4]);

        return mod;
    }
};
