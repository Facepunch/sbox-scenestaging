MODES
{
    Default();
}

CS
{
    #include "Shaders/marching_cubes/common.hlsl"

    RWStructuredBuffer<float3> VertexBuffer < Attribute("VertexBuffer"); > ;
    RWStructuredBuffer<uint> VertexCount < Attribute("VertexCount"); > ;
    uint VertexCountOffset < Attribute("VertexCountOffset"); > ;
    RWStructuredBuffer<uint> VertexIndexMap < Attribute("VertexIndexMap"); > ;

    uint AppendVertex( float3 vertex )
    {
        uint index;
        InterlockedAdd(VertexCount[VertexCountOffset], 1, index);

        VertexBuffer[VertexBufferOffset + index] = vertex;

        return index;
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        uint3 index = VoxelOffset + dispatchId;

        float a = GetVoxel(index + uint3(0, 0, 0)).Value - 127.0;
        float b = GetVoxel(index + uint3(1, 0, 0)).Value - 127.0;
        float c = GetVoxel(index + uint3(0, 1, 0)).Value - 127.0;
        float e = GetVoxel(index + uint3(0, 0, 1)).Value - 127.0;

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
