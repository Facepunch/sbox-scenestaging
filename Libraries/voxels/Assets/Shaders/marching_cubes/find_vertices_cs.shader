MODES
{
    Default();
}

CS
{
    #include "Shaders/marching_cubes/common.hlsl"

    RWStructuredBuffer<RenderVertex> VertexBuffer < Attribute("VertexBuffer"); > ;
    RWStructuredBuffer<float3> PhysicsVertexBuffer < Attribute("PhysicsVertexBuffer"); > ;
    RWStructuredBuffer<uint> VertexIndexMap < Attribute("VertexIndexMap"); > ;
    float VoxelScale < Attribute("VoxelScale"); > ;

    float SampleGrid3(in float samples[64], float3 pos)
    {
        uint3 min = uint3(floor(pos));
        uint3 max = min + 1;

        float localSamples[8];

        localSamples[0] = samples[min.x + min.y * 4 + min.z * 16];
        localSamples[1] = samples[max.x + min.y * 4 + min.z * 16];
        localSamples[2] = samples[min.x + max.y * 4 + min.z * 16];
        localSamples[3] = samples[max.x + max.y * 4 + min.z * 16];
        localSamples[4] = samples[min.x + min.y * 4 + max.z * 16];
        localSamples[5] = samples[max.x + min.y * 4 + max.z * 16];
        localSamples[6] = samples[min.x + max.y * 4 + max.z * 16];
        localSamples[7] = samples[max.x + max.y * 4 + max.z * 16];

        float3 t = pos - min;

        localSamples[0] = lerp(localSamples[0], localSamples[1], t.x);
        localSamples[1] = lerp(localSamples[2], localSamples[3], t.x);
        localSamples[2] = lerp(localSamples[4], localSamples[5], t.x);
        localSamples[3] = lerp(localSamples[6], localSamples[7], t.x);

        return lerp(
            lerp(localSamples[0], localSamples[1], t.y),
            lerp(localSamples[2], localSamples[3], t.y),
            t.z);
    }

    float3 GetNormal(float3 pos)
    {
        int3 min = int3(floor(pos)) - 1;

        float samples[64];

        int i = 0;

        for (int dz = 0; dz <= 3; dz++)
        {
            for (int dy = 0; dy <= 3; dy++)
            {
                for (int dx = 0; dx <= 3; dx++)
                {
                    samples[i++] = GetVoxel(VoxelOffset + min + int3(dx, dy, dz)).Value;
                }
            }
        }

        float3 localPos = pos - min;

        float negX = SampleGrid3(samples, localPos - float3(1.0, 0, 0));
        float posX = SampleGrid3(samples, localPos + float3(1.0, 0, 0));

        float negY = SampleGrid3(samples, localPos - float3(0, 1.0, 0));
        float posY = SampleGrid3(samples, localPos + float3(0, 1.0, 0));

        float negZ = SampleGrid3(samples, localPos - float3(0, 0, 1.0));
        float posZ = SampleGrid3(samples, localPos + float3(0, 0, 1.0));

        return normalize(float3(negX - posX, negY - posY, negZ - posZ));
    }

    uint AppendVertex(float3 pos)
    {
        uint index;
        InterlockedAdd(ResultBuffer[ResultBufferOffset], 1, index);

        RenderVertex v;

        v.Position = pos * VoxelScale;
        v.Normal = GetNormal(pos);

        VertexBuffer[VertexBufferOffset + index] = v;
        PhysicsVertexBuffer[VertexBufferOffset + index] = pos * VoxelScale;

        return index;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        uint3 index = VoxelOffset + dispatchId;

        float a = GetVoxel(index + uint3(0, 0, 0)).Value - 127.5;
        float b = GetVoxel(index + uint3(1, 0, 0)).Value - 127.5;
        float c = GetVoxel(index + uint3(0, 1, 0)).Value - 127.5;
        float e = GetVoxel(index + uint3(0, 0, 1)).Value - 127.5;

        uint baseMapOffset = VertexBufferOffset + GetVertexIndexMapIndex(dispatchId) * 3;

        if (a * b < 0)
        {
            VertexIndexMap[baseMapOffset + 0] = AppendVertex(dispatchId + float3(a / (a - b), 0, 0));
        }

        if (a * c < 0)
        {
            VertexIndexMap[baseMapOffset + 1] = AppendVertex(dispatchId + float3(0, a / (a - c), 0));
        }

        if (a * e < 0)
        {
            VertexIndexMap[baseMapOffset + 2] = AppendVertex(dispatchId + float3(0, 0, a / (a - e)));
        }
    }
}
