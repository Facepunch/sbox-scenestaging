struct CaveModification
{
    float3 WorldOrigin;
    float3 WorldSize;
    float4 WorldRotation;
    float3 NoiseOffset;

    float Smooth(float value)
    {
        return 0.5 - cos(saturate(value) * 3.14159265) * 0.5;
    }

    void Apply(VoxelColumn c)
    {
        float2 worldPos2d = c.GetWorldPos(0).xy;
        float caveyness = saturate(1.0 - length(worldPos2d - WorldOrigin.xy) * 2.0 / WorldSize.x);

        if (caveyness <= 0) return;

        float floor = WorldOrigin.z - WorldSize.z * 0.5;
        float ceiling = floor + WorldSize.z;

        for (int z = 0; z < c.Count; z++)
        {
            float3 worldPos = c.GetWorldPos(z);
            float bounds = pow(caveyness * saturate(min(worldPos.z - floor, ceiling - worldPos.z) * 2.0 / WorldSize.z), 0.25);

            if (bounds <= 0.0) continue;
            
            float density = pow(saturate(FractalSimplexNoise3D(NoiseOffset + (worldPos - WorldOrigin) / 1024.0, 4) + 0.65), 0.25);

            Voxel v = c.Get(z);

            v.Solidity = min(v.Solidity, saturate(1.0 - bounds * density));

            c.Set(z, v);
        }
    }

    static CaveModification Read(uint offset)
    {
        CaveModification mod;

        mod.WorldOrigin = float3(
            asfloat(ParameterData[offset + 0]),
            asfloat(ParameterData[offset + 1]),
            asfloat(ParameterData[offset + 2]));

        mod.WorldSize = float3(
            asfloat(ParameterData[offset + 3]),
            asfloat(ParameterData[offset + 4]),
            asfloat(ParameterData[offset + 5]));

        mod.WorldRotation = float4(
            asfloat(ParameterData[offset + 6]),
            asfloat(ParameterData[offset + 7]),
            asfloat(ParameterData[offset + 8]),
            asfloat(ParameterData[offset + 9]));

        mod.NoiseOffset = float3(
            asfloat(ParameterData[offset + 10]),
            asfloat(ParameterData[offset + 11]),
            asfloat(ParameterData[offset + 12]));

        return mod;
    }
};
