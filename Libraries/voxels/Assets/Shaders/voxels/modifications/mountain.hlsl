struct MountainModification
{
    float3 WorldOrigin;
    float WorldRadius;
    float WorldHeight;
    float NoiseOffset;

    float Smooth(float value)
    {
        return 0.5 - cos(saturate(value) * 3.14159265) * 0.5;
    }

    void Apply(VoxelColumn c)
    {
        float2 worldPos2d = c.GetWorldPos(0).xy;
        float peakiness = saturate(1.0 - length(worldPos2d - WorldOrigin.xy) / WorldRadius);

        if (peakiness <= 0) return;

        float detail = FractalSimplexNoise3D(float3((worldPos2d - WorldOrigin.xy) / 8192.0, NoiseOffset), 6) * 0.5 + 0.5;

        peakiness = lerp(0, 0.5 + detail * 0.5, peakiness);
        peakiness = Smooth(peakiness);

        float surface = WorldOrigin.z + WorldHeight * peakiness;

        for (int z = 0; z < c.Count; z++)
        {
            float worldZ = c.GetWorldPos(z).z;

            c.Apply(z, 0, worldZ - surface);
        }
    }

    static MountainModification Read(uint offset)
    {
        MountainModification mod;

        mod.WorldOrigin = float3(
            asfloat(ParameterData[offset + 0]),
            asfloat(ParameterData[offset + 1]),
            asfloat(ParameterData[offset + 2]));

        mod.WorldRadius = asfloat(ParameterData[offset + 3]);
        mod.WorldHeight = asfloat(ParameterData[offset + 4]);
        mod.NoiseOffset = asfloat(ParameterData[offset + 5]);

        return mod;
    }
};
