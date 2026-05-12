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

    RWStructuredBuffer<RenderVertex> VertexBuffer < Attribute("VertexBuffer"); > ;

    // RWStructuredBuffer<RenderVertex> IndexBuffer < Attribute("IndexBuffer"); > ;
    // uint IndexBufferOffset < Attribute("IndexBufferOffset"); > ;

    // uint GetVertexIndex(uint3 pos, LookupVertex vert)
    // {
    //     return VertexIndexMap[VertexBufferOffset + GetVoxelIndex(pos + vert.Offset) * 3 + vert.Edge];
    // }

    static const uint3 EdgeOffsets[3] = {
        uint3(1, 0, 0),
        uint3(0, 1, 0),
        uint3(0, 0, 1)
    };

    RenderVertex GenerateVertex(uint3 pos, LookupVertex vert)
    {
        RenderVertex v;

        float a = GetVoxel(pos + vert.Offset).Value - 127.5;
        float b = GetVoxel(pos + vert.Offset + EdgeOffsets[vert.Edge]).Value - 127.5;

        v.Position = pos + vert.Offset + EdgeOffsets[vert.Edge] * saturate(a / (a - b));
        v.Normal = float3(0, 0, 1);
        v.Tangent = float4(1, 0, 0, 1);

        return v;
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

        baseIndex += VertexBufferOffset;

        [unroll(5)]
        for (int i = 0; i < entry.TriangleCount; i++)
        {
            LookupTriangle tri = entry.Triangles[i];

            RenderVertex a = GenerateVertex(index, tri.Vertices[0]);
            RenderVertex b = GenerateVertex(index, tri.Vertices[1]);
            RenderVertex c = GenerateVertex(index, tri.Vertices[2]);

            float3 normal = normalize(cross(b.Position - a.Position, c.Position - a.Position));

            a.Normal = normal;
            b.Normal = normal;
            c.Normal = normal;

            VertexBuffer[baseIndex + i * 3 + 0] = a;
            VertexBuffer[baseIndex + i * 3 + 1] = b;
            VertexBuffer[baseIndex + i * 3 + 2] = c;
        }
    }
}
