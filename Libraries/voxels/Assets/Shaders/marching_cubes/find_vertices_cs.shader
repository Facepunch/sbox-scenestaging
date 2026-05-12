MODES
{
    Default();
}

CS
{
    #include "Shaders/marching_cubes/common.hlsl"

    RWStructuredBuffer<RenderVertex> VertexBuffer < Attribute("VertexBuffer"); > ;
    RWStructuredBuffer<uint> VertexIndexMap < Attribute("VertexIndexMap"); > ;

    uint AppendVertex( float3 pos )
    {
        uint index;
        InterlockedAdd(ResultBuffer[ResultBufferOffset], 1, index);

        RenderVertex v;

        v.Position = pos;
        v.Normal = float3(0, 0, 1);
        v.Tangent = float4(1, 0, 0, 1);

        VertexBuffer[VertexBufferOffset + index] = v;

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

        uint baseMapOffset = VertexBufferOffset + GetVoxelIndex(index) * 3;

        if (a * b < 0)
        {
            VertexIndexMap[baseMapOffset + 0] = AppendVertex(dispatchId + float3(a / (b - a), 0, 0));
        }

        if (a * c < 0)
        {
            VertexIndexMap[baseMapOffset + 1] = AppendVertex(dispatchId + float3(0, a / (c - a), 0));
        }

        if (a * e < 0)
        {
            VertexIndexMap[baseMapOffset + 2] = AppendVertex(dispatchId + float3(0, 0, a / (e - a)));
        }
    }
}
