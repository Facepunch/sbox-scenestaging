MODES
{
    Default();
}

CS
{
    #include "Shaders/marching_cubes/common.hlsl"

    struct LookupVertex
    {
        uint3 Offset;
        uint Edge;
    };

    struct LookupTriangle
    {
        LookupVertex Vertices[3];
    };

    struct LookupEntry
    {
        uint TriangleCount;
        LookupTriangle Triangles[5];
    };

    StructuredBuffer<LookupEntry> MarchingCubesLookup < Attribute("MarchingCubesLookup"); > ;
    StructuredBuffer<uint> VertexIndexMap < Attribute("VertexIndexMap"); > ;

    RWStructuredBuffer<uint> IndexBuffer < Attribute("IndexBuffer"); > ;
    uint IndexBufferOffset < Attribute("IndexBufferOffset"); > ;

    uint GetVertexIndex(uint3 pos, LookupVertex vert)
    {
        return VertexIndexMap[VertexBufferOffset + GetVoxelIndex(pos + vert.Offset) * 3 + vert.Edge];
    }

    [numthreads( 1, 1, 1 )]
    void MainCs(uint3 dispatchId : SV_DispatchThreadID)
    {
        uint3 index = VoxelOffset + dispatchId;

        uint aRaw = GetVoxel(index + uint3(0, 0, 0)).Value;
        uint bRaw = GetVoxel(index + uint3(1, 0, 0)).Value;
        uint cRaw = GetVoxel(index + uint3(0, 1, 0)).Value;
        uint dRaw = GetVoxel(index + uint3(1, 1, 0)).Value;
        uint eRaw = GetVoxel(index + uint3(0, 0, 1)).Value;
        uint fRaw = GetVoxel(index + uint3(1, 0, 1)).Value;
        uint gRaw = GetVoxel(index + uint3(0, 1, 1)).Value;
        uint hRaw = GetVoxel(index + uint3(1, 1, 1)).Value;

        uint a = select(aRaw >= 128, CubeVertices_A, CubeVertices_None);
        uint b = select(bRaw >= 128, CubeVertices_B, CubeVertices_None);
        uint c = select(cRaw >= 128, CubeVertices_C, CubeVertices_None);
        uint d = select(dRaw >= 128, CubeVertices_D, CubeVertices_None);
        uint e = select(eRaw >= 128, CubeVertices_E, CubeVertices_None);
        uint f = select(fRaw >= 128, CubeVertices_F, CubeVertices_None);
        uint g = select(gRaw >= 128, CubeVertices_G, CubeVertices_None);
        uint h = select(hRaw >= 128, CubeVertices_H, CubeVertices_None);

        uint lookupIndex = a | b | c | d | e | f | g | h;

        LookupEntry entry = MarchingCubesLookup[lookupIndex];

        uint baseIndex;
        InterlockedAdd(ResultBuffer[ResultBufferOffset], entry.TriangleCount * 3, baseIndex);

        baseIndex += IndexBufferOffset;

        [unroll(5)]
        for (int i = 0; i < entry.TriangleCount; i++)
        {
            LookupTriangle tri = entry.Triangles[i];

            IndexBuffer[baseIndex + i * 3 + 0] = GetVertexIndex(dispatchId, tri.Vertices[0]);
            IndexBuffer[baseIndex + i * 3 + 1] = GetVertexIndex(dispatchId, tri.Vertices[1]);
            IndexBuffer[baseIndex + i * 3 + 2] = GetVertexIndex(dispatchId, tri.Vertices[2]);
        }
    }
}
