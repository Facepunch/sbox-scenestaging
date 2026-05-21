struct PlaneModification
{
    uint Operation;
    float3 WorldNormal;
    float WorldDistance;

    void Apply(VoxelColumn c)
    {
        for (uint z = 0; z < c.Count; z++)
        {
            float3 worldPos = c.GetWorldPos(z);
            float worldDistance = dot(worldPos, WorldNormal) - WorldDistance;

            c.Apply(z, Operation, worldDistance);
        }
    }

    static PlaneModification Read(uint offset)
    {
        PlaneModification mod;

        mod.Operation = ParameterData[offset + 0];
        mod.WorldNormal = float3(
            asfloat(ParameterData[offset + 1]),
            asfloat(ParameterData[offset + 2]),
            asfloat(ParameterData[offset + 3]));
        mod.WorldDistance = asfloat(ParameterData[offset + 4]);

        return mod;
    }
};
